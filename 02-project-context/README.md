# Give the same application useful context

Work from the reference repository root. Read the supplied steering document
before enabling it. Copy its contents into a new project-specific file under
`.kiro/steering/`, creating that directory if necessary. In a generated practice
workspace, use its shorter tests/ command rather than the reference path.

Ask Kiro to list the current rules and the check command it will use. Compare
the answer with the file. Modify a nonfunctional comment and run the checks.
Useful context is an instruction aid; it is not a permission boundary.

The optional hook in `.kiro/hooks/check-consent.json` is a Kiro v1 hook-format
example. To activate it at the reference root, copy it into the root
`.kiro/hooks/` directory after reviewing the command. Change its project path if
using the separate practice root. Keep existing hook files and add this as a
separate file rather than replacing your configuration.

Trigger one agent edit to a C# file and inspect the hook result in Kiro. Also run
the same command manually. Automated command execution was locally verified;
actual hook execution in your signed-in Kiro installation needs this UI check.
If it does not run, check your Kiro version, matcher, active workspace, and the
official [hook documentation](https://kiro.dev/docs/hooks/).

Remove or disable the copied hook to stop the automation. No cloud resources
are created. Do not grant broad shell access just to remove an approval prompt.
