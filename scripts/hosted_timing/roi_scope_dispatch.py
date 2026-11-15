"""Keep legacy and family diagnostic scopes independently pinned."""
def validate_scope(plan):
    kind = plan.get("diagnostic_kind")
    if kind == "candle-family-native-pilot":
        from .candle_family_roi import validate_scope as check
    else:
        assert kind is None, "Unknown diagnostic scope"
        from .roi_followup import validate_scope as check
    check(plan)
