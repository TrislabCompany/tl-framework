# Scripts

Repetitive work in this repository is done by these scripts. Each one prints a summary and its errors, and its full help with `Get-Help <script> -Full` (or `pwsh <script> -?`). A skill or doc that needs one names it.

| Script | When | What it does |
| --- | --- | --- |
| `port-upstream.ps1` | Porting upstream commits by patch ([docs/upstream.md](../docs/upstream.md), section 1, step 4) | Per commit: `git format-patch` from their clone, maps the paths with section 2, leaves out the files with no place here and says why, renames the names with `rename-from-fsh.ps1 -Patch`, then `git am -3`. Stops at a conflict; `-NoApply` only writes the patches |
| `rename-from-fsh.ps1` | Run by `port-upstream.ps1` on each patch; on its own only to rename one patch file | The rename rules from upstream's names to Trislab's (`Trislab.Framework.*` → `Trislab.Framework.*`, `AddHero*` → `AddTrislab*`, the `tl` prefix of wire names → `tl`, …). Without `-Patch` it renames the working tree, which was done twice (the names, then the wire and brand names) |
| `affected-tests.ps1` | Before every PR, instead of the whole suite; `ci / ci` runs it on each PR | Finds the tests the branch affects: test projects through `ProjectReference`, integration-test areas through `src/Tests/affected-map.json`, the changed npm packages. Everything for shared build files, core building blocks and the test infrastructure. Prints the plan; `-Run` builds once and runs it |
| `test-host.ps1` | Running the test host as a container: the performance run (`tests/perf/`), the security scan, by hand | `up` builds the image (`src/Tests/TestHost/Dockerfile`), starts it with Postgres and storage from `compose.yaml` and waits for `/readyz`; `token` prints an administrator's access token; `down` removes everything; `logs` |
| `check-zap-report.ps1` | After a ZAP scan of the test host (`.github/workflows/security-scan.yml`, `tests/security/README.md`) | Reads ZAP's JSON report and fails on a finding of medium risk or higher that is not accepted, with a reason, in `tests/security/zap-accepted.json`; prints the accepted ones it met and the stale entries |

Add a script when the same kind of work comes back and a script makes it faster and cheaper than doing it by hand, never for one-off work. List it here, give it comment-based help, and name it in the skill or doc that uses it.
