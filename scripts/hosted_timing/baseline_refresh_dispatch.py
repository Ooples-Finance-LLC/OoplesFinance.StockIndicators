"""Select only explicitly authorized baseline-refresh implementations."""


def implementation(plan):
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
