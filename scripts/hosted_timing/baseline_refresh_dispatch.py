"""Select only explicitly authorized baseline-refresh implementations."""


def implementation(plan):
    if plan.get('native_eligibility_eight_screen'):
        assert not any(plan.get(key) for key in ('current_baseline_ten_refresh','named_output_projection_refresh','pointwise_family_refresh'))
        from . import native_eligibility_eight_screen
        return native_eligibility_eight_screen
    if plan.get("current_baseline_ten_refresh"):
        assert not plan.get("named_output_projection_refresh") and not plan.get("pointwise_family_refresh")
        from . import current_baseline_ten_refresh
        return current_baseline_ten_refresh
    named = plan.get('named_output_projection_refresh', False)
    pointwise = plan.get('pointwise_family_refresh', False)
    assert bool(named) != bool(pointwise), 'Exactly one authenticated refresh scope is required'
    if named:
        from . import named_projection_refresh
        return named_projection_refresh
    from . import pointwise_family_refresh
    return pointwise_family_refresh


def validate_scope(plan):
    return implementation(plan).validate_scope(plan)


def verify_runtime_identity(plan, manifests):
    return implementation(plan).verify_runtime_identity(plan, manifests)
