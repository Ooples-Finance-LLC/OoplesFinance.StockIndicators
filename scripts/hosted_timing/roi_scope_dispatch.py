"""Keep legacy and family diagnostic scopes independently pinned."""
def validate_scope(plan):
    kind = plan.get("diagnostic_kind")
    if kind == "kama-current-source-native-pilot":
        from .kama_fused_prefix_roi import validate_scope as check
    elif kind == "analytic-grid-native-pilot":
        from .analytic_grid_roi import validate_scope as check
    elif kind == "modified-hikkake-sparse-replay-native-pilot":
        from .modified_hikkake_sparse_replay_roi import validate_scope as check
    elif kind == "renko-constant-tr-private-native-pilot":
        from .renko_constant_tr_private_roi import validate_scope as check
    elif kind == "renko-normalized-grid-native-pilot":
        from .renko_normalized_grid_roi import validate_scope as check
    elif kind == "sample-shape-fixed-grid-native-pilot":
        from .sample_shape_fixed_grid_roi import validate_scope as check
    elif kind == "alma-fixed-grid-native-pilot":
        from .alma_fixed_grid_roi import validate_scope as check
    elif kind == "cmf-fixed-grid-native-pilot":
        from .cmf_fixed_grid_roi import validate_scope as check
    elif kind == "ultimate-direct-long-native-pilot":
        from .ultimate_direct_long_roi import validate_scope as check
    elif kind == "ultimate-wider-long-native-pilot":
        from .ultimate_wider_long_roi import validate_scope as check
    elif kind == "lagged-five-native-pilot":
        from .lagged_five_roi import validate_scope as check
    elif kind == "ladder-word-native-pilot":
        from .ladder_word_roi import validate_scope as check
    elif kind == "candle-isolated-native-pilot":
        from .candle_isolated_roi import validate_scope as check
    elif kind == "wilder-word-ema-native-pilot":
        from .wilder_word_ema_roi import validate_scope as check
    elif kind == "candle-single-pass-native-pilot":
        from .candle_single_pass_roi import validate_scope as check
    elif kind == "candle-family-native-pilot":
        from .candle_family_roi import validate_scope as check
    elif kind == "candle-integer-window-native-pilot":
        from .candle_integer_roi import validate_scope as check
    elif kind == "candle-six-api-native-pilot":
        from .candle_six_roi import validate_scope as check
    elif kind == "pointwise-projection-native-pilot":
        from .pointwise_projection_roi import validate_scope as check
    else:
        assert kind is None, "Unknown diagnostic scope"
        from .roi_followup import validate_scope as check
    check(plan)
