-- Supabase Schema for OoplesFinance Trading Platform
-- Run this in Supabase SQL Editor to set up the database

-- Enable UUID extension if not already enabled
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================
-- USERS & SUBSCRIPTIONS
-- ============================================

-- User profiles (extends Supabase auth.users)
CREATE TABLE IF NOT EXISTS public.user_profiles (
    id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    email TEXT NOT NULL,
    display_name TEXT,
    avatar_url TEXT,
    subscription_tier TEXT NOT NULL DEFAULT 'free' CHECK (subscription_tier IN ('free', 'starter', 'pro', 'enterprise')),
    subscription_expires_at TIMESTAMPTZ,
    max_strategies INT NOT NULL DEFAULT 5,
    max_backtests_per_day INT NOT NULL DEFAULT 10,
    api_calls_remaining INT NOT NULL DEFAULT 1000,
    api_calls_reset_at TIMESTAMPTZ DEFAULT NOW() + INTERVAL '1 day',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Subscription tiers
CREATE TABLE IF NOT EXISTS public.subscription_tiers (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    price_monthly DECIMAL(10,2) NOT NULL,
    price_yearly DECIMAL(10,2) NOT NULL,
    max_strategies INT NOT NULL,
    max_backtests_per_day INT NOT NULL,
    max_api_calls_per_day INT NOT NULL,
    can_publish_strategies BOOLEAN NOT NULL DEFAULT false,
    can_use_live_trading BOOLEAN NOT NULL DEFAULT false,
    can_use_ml_features BOOLEAN NOT NULL DEFAULT false,
    support_level TEXT NOT NULL DEFAULT 'community',
    features JSONB NOT NULL DEFAULT '[]'::jsonb
);

-- Insert default subscription tiers
INSERT INTO public.subscription_tiers (id, name, price_monthly, price_yearly, max_strategies, max_backtests_per_day, max_api_calls_per_day, can_publish_strategies, can_use_live_trading, can_use_ml_features, support_level, features) VALUES
    ('free', 'Free', 0, 0, 5, 10, 1000, false, false, false, 'community', '["Visual Builder", "Basic Indicators", "Paper Trading"]'::jsonb),
    ('starter', 'Starter', 19.99, 199.99, 20, 50, 10000, true, false, false, 'email', '["All Free Features", "Premium Indicators", "Strategy Marketplace", "Export to Code"]'::jsonb),
    ('pro', 'Professional', 49.99, 499.99, 100, 500, 100000, true, true, true, 'priority', '["All Starter Features", "Live Trading", "ML Optimization", "Advanced Backtesting", "Monte Carlo"]'::jsonb),
    ('enterprise', 'Enterprise', 199.99, 1999.99, -1, -1, -1, true, true, true, 'dedicated', '["All Pro Features", "Unlimited Everything", "White-label Options", "Custom Integrations", "SLA"]'::jsonb)
ON CONFLICT (id) DO NOTHING;

-- ============================================
-- STRATEGIES
-- ============================================

-- Strategies table
CREATE TABLE IF NOT EXISTS public.strategies (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    version TEXT NOT NULL DEFAULT '1.0.0',
    content JSONB NOT NULL,
    is_public BOOLEAN NOT NULL DEFAULT false,
    is_template BOOLEAN NOT NULL DEFAULT false,
    category TEXT,
    tags TEXT[] DEFAULT '{}',
    download_count INT NOT NULL DEFAULT 0,
    rating DECIMAL(3,2) NOT NULL DEFAULT 0,
    rating_count INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    modified_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Performance metadata (updated after backtests)
    last_backtest_at TIMESTAMPTZ,
    backtest_sharpe DECIMAL(10,4),
    backtest_return DECIMAL(10,4),
    backtest_max_drawdown DECIMAL(10,4)
);

-- Strategy versions for history
CREATE TABLE IF NOT EXISTS public.strategy_versions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    strategy_id UUID NOT NULL REFERENCES public.strategies(id) ON DELETE CASCADE,
    version TEXT NOT NULL,
    content JSONB NOT NULL,
    change_notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID REFERENCES public.user_profiles(id)
);

-- Strategy ratings
CREATE TABLE IF NOT EXISTS public.strategy_ratings (
    strategy_id UUID NOT NULL REFERENCES public.strategies(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
    rating INT NOT NULL CHECK (rating >= 1 AND rating <= 5),
    review TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (strategy_id, user_id)
);

-- Strategy forks (tracking who forked what)
CREATE TABLE IF NOT EXISTS public.strategy_forks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    source_strategy_id UUID NOT NULL REFERENCES public.strategies(id) ON DELETE SET NULL,
    forked_strategy_id UUID NOT NULL REFERENCES public.strategies(id) ON DELETE CASCADE,
    forked_by UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
    forked_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================
-- BACKTESTS
-- ============================================

-- Backtest runs
CREATE TABLE IF NOT EXISTS public.backtests (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
    strategy_id UUID NOT NULL REFERENCES public.strategies(id) ON DELETE CASCADE,
    status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'running', 'completed', 'failed')),

    -- Configuration
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    symbols TEXT[] NOT NULL,
    initial_capital DECIMAL(18,2) NOT NULL DEFAULT 100000,
    commission DECIMAL(10,4) NOT NULL DEFAULT 0,
    slippage DECIMAL(10,4) NOT NULL DEFAULT 0,

    -- Results (populated after completion)
    total_return DECIMAL(10,4),
    annual_return DECIMAL(10,4),
    sharpe_ratio DECIMAL(10,4),
    sortino_ratio DECIMAL(10,4),
    max_drawdown DECIMAL(10,4),
    win_rate DECIMAL(10,4),
    profit_factor DECIMAL(10,4),
    total_trades INT,

    -- Full results JSON
    results JSONB,

    -- Timing
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    error_message TEXT
);

-- ============================================
-- LIVE TRADING
-- ============================================

-- Connected broker accounts
CREATE TABLE IF NOT EXISTS public.broker_connections (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
    broker TEXT NOT NULL CHECK (broker IN ('alpaca', 'interactive_brokers', 'td_ameritrade', 'tradier', 'coinbase', 'binance')),
    account_id TEXT NOT NULL,
    account_type TEXT NOT NULL DEFAULT 'paper' CHECK (account_type IN ('paper', 'live')),
    credentials_encrypted BYTEA,
    is_active BOOLEAN NOT NULL DEFAULT true,
    last_sync_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE(user_id, broker, account_id)
);

-- Active strategies (deployed for live/paper trading)
CREATE TABLE IF NOT EXISTS public.active_strategies (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
    strategy_id UUID NOT NULL REFERENCES public.strategies(id) ON DELETE CASCADE,
    broker_connection_id UUID NOT NULL REFERENCES public.broker_connections(id) ON DELETE CASCADE,

    status TEXT NOT NULL DEFAULT 'stopped' CHECK (status IN ('stopped', 'running', 'paused', 'error')),
    is_paper BOOLEAN NOT NULL DEFAULT true,

    -- Allocation
    allocated_capital DECIMAL(18,2) NOT NULL,
    max_position_size DECIMAL(10,4) NOT NULL DEFAULT 0.10,

    -- Risk controls
    max_daily_loss DECIMAL(10,4) NOT NULL DEFAULT 0.02,
    max_drawdown DECIMAL(10,4) NOT NULL DEFAULT 0.10,

    -- Stats
    pnl_today DECIMAL(18,2) NOT NULL DEFAULT 0,
    pnl_total DECIMAL(18,2) NOT NULL DEFAULT 0,
    trades_today INT NOT NULL DEFAULT 0,
    trades_total INT NOT NULL DEFAULT 0,

    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    stopped_at TIMESTAMPTZ,
    error_message TEXT
);

-- Trade log
CREATE TABLE IF NOT EXISTS public.trades (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
    active_strategy_id UUID REFERENCES public.active_strategies(id) ON DELETE SET NULL,
    strategy_id UUID REFERENCES public.strategies(id) ON DELETE SET NULL,

    -- Trade details
    symbol TEXT NOT NULL,
    side TEXT NOT NULL CHECK (side IN ('buy', 'sell')),
    quantity DECIMAL(18,8) NOT NULL,
    price DECIMAL(18,8) NOT NULL,
    commission DECIMAL(18,8) NOT NULL DEFAULT 0,

    -- Order info
    order_id TEXT,
    order_type TEXT NOT NULL CHECK (order_type IN ('market', 'limit', 'stop', 'stop_limit')),
    fill_price DECIMAL(18,8),

    -- P&L
    realized_pnl DECIMAL(18,8),

    -- Timestamps
    signal_at TIMESTAMPTZ NOT NULL,
    submitted_at TIMESTAMPTZ,
    filled_at TIMESTAMPTZ,

    -- Status
    status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'submitted', 'filled', 'partial', 'cancelled', 'rejected')),
    rejection_reason TEXT,

    -- Metadata
    signal_reason TEXT,
    tags TEXT[] DEFAULT '{}'
);

-- ============================================
-- ALERTS & NOTIFICATIONS
-- ============================================

-- User alerts
CREATE TABLE IF NOT EXISTS public.alerts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
    strategy_id UUID REFERENCES public.strategies(id) ON DELETE CASCADE,

    type TEXT NOT NULL CHECK (type IN ('signal', 'trade', 'risk', 'system', 'price')),
    severity TEXT NOT NULL DEFAULT 'info' CHECK (severity IN ('info', 'warning', 'critical')),
    title TEXT NOT NULL,
    message TEXT NOT NULL,

    is_read BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    read_at TIMESTAMPTZ
);

-- ============================================
-- INDEXES
-- ============================================

CREATE INDEX IF NOT EXISTS idx_strategies_user_id ON public.strategies(user_id);
CREATE INDEX IF NOT EXISTS idx_strategies_is_public ON public.strategies(is_public) WHERE is_public = true;
CREATE INDEX IF NOT EXISTS idx_strategies_tags ON public.strategies USING GIN(tags);
CREATE INDEX IF NOT EXISTS idx_strategies_category ON public.strategies(category);
CREATE INDEX IF NOT EXISTS idx_strategies_rating ON public.strategies(rating DESC) WHERE is_public = true;

CREATE INDEX IF NOT EXISTS idx_backtests_user_id ON public.backtests(user_id);
CREATE INDEX IF NOT EXISTS idx_backtests_strategy_id ON public.backtests(strategy_id);
CREATE INDEX IF NOT EXISTS idx_backtests_created_at ON public.backtests(created_at DESC);

CREATE INDEX IF NOT EXISTS idx_trades_user_id ON public.trades(user_id);
CREATE INDEX IF NOT EXISTS idx_trades_active_strategy_id ON public.trades(active_strategy_id);
CREATE INDEX IF NOT EXISTS idx_trades_filled_at ON public.trades(filled_at DESC);
CREATE INDEX IF NOT EXISTS idx_trades_symbol ON public.trades(symbol);

CREATE INDEX IF NOT EXISTS idx_alerts_user_id_unread ON public.alerts(user_id) WHERE is_read = false;

-- ============================================
-- ROW LEVEL SECURITY
-- ============================================

ALTER TABLE public.user_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.strategies ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.strategy_versions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.strategy_ratings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.backtests ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.broker_connections ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.active_strategies ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.trades ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.alerts ENABLE ROW LEVEL SECURITY;

-- User profiles: users can read/update their own profile
CREATE POLICY "Users can view own profile" ON public.user_profiles FOR SELECT USING (auth.uid() = id);
CREATE POLICY "Users can update own profile" ON public.user_profiles FOR UPDATE USING (auth.uid() = id);

-- Strategies: users can CRUD their own, can read public strategies
CREATE POLICY "Users can view own strategies" ON public.strategies FOR SELECT USING (auth.uid() = user_id);
CREATE POLICY "Users can view public strategies" ON public.strategies FOR SELECT USING (is_public = true);
CREATE POLICY "Users can create strategies" ON public.strategies FOR INSERT WITH CHECK (auth.uid() = user_id);
CREATE POLICY "Users can update own strategies" ON public.strategies FOR UPDATE USING (auth.uid() = user_id);
CREATE POLICY "Users can delete own strategies" ON public.strategies FOR DELETE USING (auth.uid() = user_id);

-- Ratings: users can rate public strategies, view all ratings on public strategies
CREATE POLICY "Users can rate public strategies" ON public.strategy_ratings FOR INSERT WITH CHECK (
    auth.uid() = user_id AND
    EXISTS (SELECT 1 FROM public.strategies WHERE id = strategy_id AND is_public = true)
);
CREATE POLICY "Users can view ratings on public strategies" ON public.strategy_ratings FOR SELECT USING (
    EXISTS (SELECT 1 FROM public.strategies WHERE id = strategy_id AND is_public = true)
);

-- Backtests: users can only access their own
CREATE POLICY "Users can CRUD own backtests" ON public.backtests FOR ALL USING (auth.uid() = user_id);

-- Broker connections: users can only access their own
CREATE POLICY "Users can CRUD own broker connections" ON public.broker_connections FOR ALL USING (auth.uid() = user_id);

-- Active strategies: users can only access their own
CREATE POLICY "Users can CRUD own active strategies" ON public.active_strategies FOR ALL USING (auth.uid() = user_id);

-- Trades: users can only access their own
CREATE POLICY "Users can CRUD own trades" ON public.trades FOR ALL USING (auth.uid() = user_id);

-- Alerts: users can only access their own
CREATE POLICY "Users can CRUD own alerts" ON public.alerts FOR ALL USING (auth.uid() = user_id);

-- ============================================
-- FUNCTIONS
-- ============================================

-- Function to increment download count
CREATE OR REPLACE FUNCTION public.increment_download_count(p_strategy_id UUID)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    UPDATE public.strategies
    SET download_count = download_count + 1
    WHERE id = p_strategy_id;
END;
$$;

-- Function to update average rating
CREATE OR REPLACE FUNCTION public.update_average_rating(p_strategy_id UUID)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_avg DECIMAL(3,2);
    v_count INT;
BEGIN
    SELECT AVG(rating)::DECIMAL(3,2), COUNT(*)
    INTO v_avg, v_count
    FROM public.strategy_ratings
    WHERE strategy_id = p_strategy_id;

    UPDATE public.strategies
    SET rating = COALESCE(v_avg, 0), rating_count = v_count
    WHERE id = p_strategy_id;
END;
$$;

-- Function to create user profile on signup
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    INSERT INTO public.user_profiles (id, email, display_name)
    VALUES (NEW.id, NEW.email, SPLIT_PART(NEW.email, '@', 1));
    RETURN NEW;
END;
$$;

-- Trigger to create profile on user signup
DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();

-- Function to update modified_at timestamp
CREATE OR REPLACE FUNCTION public.update_modified_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.modified_at = NOW();
    RETURN NEW;
END;
$$;

-- Apply updated_at trigger to strategies
DROP TRIGGER IF EXISTS strategies_updated_at ON public.strategies;
CREATE TRIGGER strategies_updated_at
    BEFORE UPDATE ON public.strategies
    FOR EACH ROW EXECUTE FUNCTION public.update_modified_at();

-- Apply updated_at trigger to user_profiles
DROP TRIGGER IF EXISTS user_profiles_updated_at ON public.user_profiles;
CREATE TRIGGER user_profiles_updated_at
    BEFORE UPDATE ON public.user_profiles
    FOR EACH ROW EXECUTE FUNCTION public.update_modified_at();

-- ============================================
-- GRANTS (for anon and authenticated users)
-- ============================================

GRANT USAGE ON SCHEMA public TO anon, authenticated;

GRANT SELECT ON public.subscription_tiers TO anon, authenticated;

GRANT SELECT, INSERT, UPDATE ON public.user_profiles TO authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.strategies TO authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.strategy_versions TO authenticated;
GRANT SELECT, INSERT ON public.strategy_ratings TO authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.backtests TO authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.broker_connections TO authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.active_strategies TO authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.trades TO authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.alerts TO authenticated;

GRANT EXECUTE ON FUNCTION public.increment_download_count TO authenticated;
GRANT EXECUTE ON FUNCTION public.update_average_rating TO authenticated;

-- ============================================
-- ADDITIONAL TABLES FOR TRADING APP
-- ============================================

-- Positions table (real-time portfolio tracking)
CREATE TABLE IF NOT EXISTS public.positions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
    broker_connection_id UUID NOT NULL REFERENCES public.broker_connections(id) ON DELETE CASCADE,

    symbol TEXT NOT NULL,
    quantity DECIMAL(18,8) NOT NULL,
    average_entry_price DECIMAL(18,8) NOT NULL,
    current_price DECIMAL(18,8),
    market_value DECIMAL(18,2),
    cost_basis DECIMAL(18,2) NOT NULL,
    unrealized_pnl DECIMAL(18,2),
    unrealized_pnl_percent DECIMAL(10,4),
    day_pnl DECIMAL(18,2),

    -- Metadata
    asset_class TEXT DEFAULT 'stock' CHECK (asset_class IN ('stock', 'option', 'crypto', 'forex', 'future')),
    exchange TEXT,

    -- Timestamps
    opened_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE(user_id, broker_connection_id, symbol)
);

-- Orders table (pending/open orders, separate from filled trades)
CREATE TABLE IF NOT EXISTS public.orders (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
    broker_connection_id UUID NOT NULL REFERENCES public.broker_connections(id) ON DELETE CASCADE,
    active_strategy_id UUID REFERENCES public.active_strategies(id) ON DELETE SET NULL,

    -- Broker order ID
    broker_order_id TEXT NOT NULL,
    client_order_id TEXT,

    -- Order details
    symbol TEXT NOT NULL,
    side TEXT NOT NULL CHECK (side IN ('buy', 'sell')),
    order_type TEXT NOT NULL CHECK (order_type IN ('market', 'limit', 'stop', 'stop_limit', 'trailing_stop')),
    time_in_force TEXT NOT NULL DEFAULT 'day' CHECK (time_in_force IN ('day', 'gtc', 'ioc', 'fok', 'opg', 'cls')),

    -- Quantities
    quantity DECIMAL(18,8) NOT NULL,
    filled_quantity DECIMAL(18,8) NOT NULL DEFAULT 0,

    -- Prices
    limit_price DECIMAL(18,8),
    stop_price DECIMAL(18,8),
    average_fill_price DECIMAL(18,8),
    trail_percent DECIMAL(10,4),
    trail_price DECIMAL(18,8),

    -- Status
    status TEXT NOT NULL DEFAULT 'new' CHECK (status IN ('new', 'pending_new', 'accepted', 'pending_cancel', 'partial', 'filled', 'done_for_day', 'cancelled', 'expired', 'rejected', 'replaced')),
    rejection_reason TEXT,

    -- Extended hours
    extended_hours BOOLEAN NOT NULL DEFAULT false,

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    submitted_at TIMESTAMPTZ,
    filled_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,
    expired_at TIMESTAMPTZ,

    -- Metadata
    notes TEXT,
    tags TEXT[] DEFAULT '{}'
);

-- Price alerts table (specific for price-based alerts)
CREATE TABLE IF NOT EXISTS public.price_alerts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,

    symbol TEXT NOT NULL,
    condition TEXT NOT NULL CHECK (condition IN ('above', 'below', 'crosses_above', 'crosses_below', 'percent_change')),
    target_price DECIMAL(18,8),
    percent_threshold DECIMAL(10,4),

    -- Reference price (for percent change alerts)
    reference_price DECIMAL(18,8),

    -- Status
    is_active BOOLEAN NOT NULL DEFAULT true,
    is_triggered BOOLEAN NOT NULL DEFAULT false,
    triggered_at TIMESTAMPTZ,
    triggered_price DECIMAL(18,8),

    -- Notification settings
    notify_push BOOLEAN NOT NULL DEFAULT true,
    notify_email BOOLEAN NOT NULL DEFAULT false,
    notify_sms BOOLEAN NOT NULL DEFAULT false,

    -- Recurrence
    is_recurring BOOLEAN NOT NULL DEFAULT false,
    cooldown_minutes INT DEFAULT 60,
    last_notified_at TIMESTAMPTZ,

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ,

    -- Notes
    notes TEXT
);

-- Audit log table (all user actions with timestamps)
CREATE TABLE IF NOT EXISTS public.audit_log (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,

    -- Action details
    action TEXT NOT NULL,
    resource_type TEXT NOT NULL,
    resource_id UUID,

    -- Changes (JSON diff)
    old_values JSONB,
    new_values JSONB,

    -- Context
    ip_address INET,
    user_agent TEXT,

    -- Timestamp
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- User settings table
CREATE TABLE IF NOT EXISTS public.user_settings (
    user_id UUID PRIMARY KEY REFERENCES public.user_profiles(id) ON DELETE CASCADE,

    -- Display settings
    theme TEXT NOT NULL DEFAULT 'dark' CHECK (theme IN ('light', 'dark', 'system')),
    timezone TEXT NOT NULL DEFAULT 'America/New_York',
    date_format TEXT NOT NULL DEFAULT 'MM/dd/yyyy',
    number_format TEXT NOT NULL DEFAULT 'en-US',

    -- Trading defaults
    default_order_type TEXT NOT NULL DEFAULT 'market' CHECK (default_order_type IN ('market', 'limit')),
    default_time_in_force TEXT NOT NULL DEFAULT 'day' CHECK (default_time_in_force IN ('day', 'gtc')),
    confirm_orders BOOLEAN NOT NULL DEFAULT true,
    show_extended_hours BOOLEAN NOT NULL DEFAULT false,

    -- Notification settings
    push_notifications BOOLEAN NOT NULL DEFAULT true,
    email_notifications BOOLEAN NOT NULL DEFAULT true,
    notify_order_fills BOOLEAN NOT NULL DEFAULT true,
    notify_price_alerts BOOLEAN NOT NULL DEFAULT true,
    notify_strategy_signals BOOLEAN NOT NULL DEFAULT true,
    notify_risk_warnings BOOLEAN NOT NULL DEFAULT true,

    -- Security
    require_biometric BOOLEAN NOT NULL DEFAULT false,
    session_timeout_minutes INT NOT NULL DEFAULT 30,

    -- Timestamps
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Watchlist table
CREATE TABLE IF NOT EXISTS public.watchlists (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES public.user_profiles(id) ON DELETE CASCADE,
    name TEXT NOT NULL DEFAULT 'My Watchlist',
    symbols TEXT[] NOT NULL DEFAULT '{}',
    is_default BOOLEAN NOT NULL DEFAULT false,
    sort_order INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================
-- ADDITIONAL INDEXES
-- ============================================

CREATE INDEX IF NOT EXISTS idx_positions_user_id ON public.positions(user_id);
CREATE INDEX IF NOT EXISTS idx_positions_symbol ON public.positions(symbol);
CREATE INDEX IF NOT EXISTS idx_positions_broker ON public.positions(broker_connection_id);

CREATE INDEX IF NOT EXISTS idx_orders_user_id ON public.orders(user_id);
CREATE INDEX IF NOT EXISTS idx_orders_status ON public.orders(status) WHERE status NOT IN ('filled', 'cancelled', 'expired', 'rejected');
CREATE INDEX IF NOT EXISTS idx_orders_symbol ON public.orders(symbol);
CREATE INDEX IF NOT EXISTS idx_orders_broker_order_id ON public.orders(broker_order_id);
CREATE INDEX IF NOT EXISTS idx_orders_created_at ON public.orders(created_at DESC);

CREATE INDEX IF NOT EXISTS idx_price_alerts_user_active ON public.price_alerts(user_id) WHERE is_active = true;
CREATE INDEX IF NOT EXISTS idx_price_alerts_symbol ON public.price_alerts(symbol) WHERE is_active = true;

CREATE INDEX IF NOT EXISTS idx_audit_log_user_id ON public.audit_log(user_id);
CREATE INDEX IF NOT EXISTS idx_audit_log_created_at ON public.audit_log(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_audit_log_action ON public.audit_log(action);

CREATE INDEX IF NOT EXISTS idx_watchlists_user_id ON public.watchlists(user_id);

-- ============================================
-- ADDITIONAL RLS POLICIES
-- ============================================

ALTER TABLE public.positions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.orders ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.price_alerts ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.audit_log ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.user_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.watchlists ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Users can CRUD own positions" ON public.positions FOR ALL USING (auth.uid() = user_id);
CREATE POLICY "Users can CRUD own orders" ON public.orders FOR ALL USING (auth.uid() = user_id);
CREATE POLICY "Users can CRUD own price alerts" ON public.price_alerts FOR ALL USING (auth.uid() = user_id);
CREATE POLICY "Users can view own audit log" ON public.audit_log FOR SELECT USING (auth.uid() = user_id);
CREATE POLICY "Users can CRUD own settings" ON public.user_settings FOR ALL USING (auth.uid() = user_id);
CREATE POLICY "Users can CRUD own watchlists" ON public.watchlists FOR ALL USING (auth.uid() = user_id);

-- ============================================
-- ADDITIONAL GRANTS
-- ============================================

GRANT SELECT, INSERT, UPDATE, DELETE ON public.positions TO authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.orders TO authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.price_alerts TO authenticated;
GRANT SELECT, INSERT ON public.audit_log TO authenticated;
GRANT SELECT, INSERT, UPDATE ON public.user_settings TO authenticated;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.watchlists TO authenticated;

-- ============================================
-- HELPER FUNCTIONS
-- ============================================

-- Function to calculate portfolio value for a user
CREATE OR REPLACE FUNCTION public.get_portfolio_value(p_user_id UUID, p_broker_connection_id UUID DEFAULT NULL)
RETURNS TABLE (
    total_value DECIMAL(18,2),
    total_cost_basis DECIMAL(18,2),
    total_unrealized_pnl DECIMAL(18,2),
    total_day_pnl DECIMAL(18,2),
    position_count INT
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    RETURN QUERY
    SELECT
        COALESCE(SUM(market_value), 0)::DECIMAL(18,2) as total_value,
        COALESCE(SUM(cost_basis), 0)::DECIMAL(18,2) as total_cost_basis,
        COALESCE(SUM(unrealized_pnl), 0)::DECIMAL(18,2) as total_unrealized_pnl,
        COALESCE(SUM(day_pnl), 0)::DECIMAL(18,2) as total_day_pnl,
        COUNT(*)::INT as position_count
    FROM public.positions
    WHERE user_id = p_user_id
    AND (p_broker_connection_id IS NULL OR broker_connection_id = p_broker_connection_id);
END;
$$;

-- Function to log audit event
CREATE OR REPLACE FUNCTION public.log_audit_event(
    p_user_id UUID,
    p_action TEXT,
    p_resource_type TEXT,
    p_resource_id UUID DEFAULT NULL,
    p_old_values JSONB DEFAULT NULL,
    p_new_values JSONB DEFAULT NULL
)
RETURNS UUID
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_audit_id UUID;
BEGIN
    INSERT INTO public.audit_log (user_id, action, resource_type, resource_id, old_values, new_values)
    VALUES (p_user_id, p_action, p_resource_type, p_resource_id, p_old_values, p_new_values)
    RETURNING id INTO v_audit_id;

    RETURN v_audit_id;
END;
$$;

-- Function to check and trigger price alerts
CREATE OR REPLACE FUNCTION public.check_price_alert(
    p_symbol TEXT,
    p_current_price DECIMAL(18,8)
)
RETURNS SETOF public.price_alerts
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    RETURN QUERY
    UPDATE public.price_alerts
    SET
        is_triggered = true,
        triggered_at = NOW(),
        triggered_price = p_current_price,
        is_active = CASE WHEN is_recurring THEN true ELSE false END,
        last_notified_at = NOW()
    WHERE symbol = p_symbol
    AND is_active = true
    AND is_triggered = false
    AND (
        (condition = 'above' AND p_current_price >= target_price) OR
        (condition = 'below' AND p_current_price <= target_price) OR
        (condition = 'percent_change' AND reference_price IS NOT NULL AND
            ABS((p_current_price - reference_price) / reference_price) * 100 >= percent_threshold)
    )
    AND (expires_at IS NULL OR expires_at > NOW())
    AND (last_notified_at IS NULL OR last_notified_at + (cooldown_minutes || ' minutes')::INTERVAL < NOW())
    RETURNING *;
END;
$$;

-- Trigger to update positions.last_updated_at
CREATE OR REPLACE FUNCTION public.update_position_timestamp()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.last_updated_at = NOW();
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS positions_updated_at ON public.positions;
CREATE TRIGGER positions_updated_at
    BEFORE UPDATE ON public.positions
    FOR EACH ROW EXECUTE FUNCTION public.update_position_timestamp();

-- Trigger to update watchlists.updated_at
DROP TRIGGER IF EXISTS watchlists_updated_at ON public.watchlists;
CREATE TRIGGER watchlists_updated_at
    BEFORE UPDATE ON public.watchlists
    FOR EACH ROW EXECUTE FUNCTION public.update_modified_at();

-- Trigger to update user_settings.updated_at
DROP TRIGGER IF EXISTS user_settings_updated_at ON public.user_settings;
CREATE TRIGGER user_settings_updated_at
    BEFORE UPDATE ON public.user_settings
    FOR EACH ROW EXECUTE FUNCTION public.update_modified_at();

GRANT EXECUTE ON FUNCTION public.get_portfolio_value TO authenticated;
GRANT EXECUTE ON FUNCTION public.log_audit_event TO authenticated;
GRANT EXECUTE ON FUNCTION public.check_price_alert TO authenticated;
