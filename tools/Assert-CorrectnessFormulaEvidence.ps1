param([Parameter(Mandatory = $true)][xml]$Evidence)
$ErrorActionPreference = 'Stop'

# A passing fixture run with recurrence-only or post-warmup-only references is insufficient for release.
$cases = @($Evidence.correctnessEvidence.case)
if ($cases.Count -eq 0) { throw 'No formula evidence was supplied.' }
foreach ($case in $cases) {
    if ($case.passed -ne 'True') { throw "Failing formula evidence: $($case.name)." }
    foreach ($attribute in @('recurrenceOnlySlots', 'missingStartupSlots', 'independentTrajectorySlots')) {
        if (-not $case.HasAttribute($attribute)) { throw "Missing $attribute evidence: $($case.name)." }
    }
    if (-not [string]::IsNullOrEmpty($case.recurrenceOnlySlots)) {
        throw "Independent trajectory evidence is incomplete: $($case.name), slots $($case.recurrenceOnlySlots)."
    }
    if (-not [string]::IsNullOrEmpty($case.missingStartupSlots)) {
        throw "Startup formula evidence is incomplete: $($case.name), slots $($case.missingStartupSlots)."
    }
    if ([string]::IsNullOrWhiteSpace($case.independentTrajectorySlots)) {
        throw "No independent trajectory output slots: $($case.name)."
    }
}
