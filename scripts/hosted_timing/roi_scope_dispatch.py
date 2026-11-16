"""Keep legacy and family diagnostic scopes independently pinned."""
def validate_scope(plan):
    kind = plan.get("diagnostic_kind")
    if kind == "three-stars-blocks-native-pilot":
        from .three_stars_blocks_roi import validate_scope as check
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
