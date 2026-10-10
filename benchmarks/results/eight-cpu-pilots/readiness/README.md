# PR 250 readiness checks

The four stale SMA mutation sites now target the guarded shared kernel. The
isolated unchanged baseline passed; wrong divisor, lost exact window, wrong
eviction lag and bypassed overflow fallback were each killed by completed failing
tests. `mutations.json.gz` records the selected campaign and source hashes. This
is evidence for those four faults, not a complete mutation-release campaign.

Nine mutation-tool tests and nine benchmark-evidence tests passed. The expanded
competitor subset passed 143 tests with no skips, including both history modes
for the eight pilots and the seven representative CPU shapes.

The benchmark harness now retains owned builder results for the same lifetime
boundary as competitor results. Full and LatestOnly are separate methods in the
eight-family, representative CPU and GPU campaigns. Multiple-period native SMA
retains both result arrays. GPU setup checks both history modes, exact certified
SMA/presence bits, signed zero, the Asin tolerance and the actual selected backend.

Two captured review findings are addressed: reusable-workload explanations now
identify the correct competitor, and unrelated label events have different
workflow concurrency keys. Sonar findings preserve exact discrete checks and
explicitly document seeded fixture randomness rather than weakening assertions.
