"""Keep legacy and family diagnostic scopes independently pinned."""
def validate_scope(plan):
    kind = plan.get("diagnostic_kind")
    if kind == "candle-family-native-pilot":
        from .candle_family_roi import validate_scope as check
    elif kind == "candle-integer-window-native-pilot":
        from .candle_integer_roi import validate_scope as check
    elif kind == "candle-six-api-native-pilot":
        from .candle_six_roi import validate_scope as check
    else:
        assert kind is None, "Unknown diagnostic scope"
        from .roi_followup import validate_scope as check
    check(plan)
