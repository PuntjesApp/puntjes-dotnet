using System.Text.Json;
using Puntjes.Tools;

switch (args)
{
    case ["codegen", var specPath, var outDir]:
        using (var spec = JsonDocument.Parse(File.ReadAllText(specPath)))
        {
            Directory.CreateDirectory(Path.Combine(outDir, "Errors"));
            Directory.CreateDirectory(Path.Combine(outDir, "Security"));
            File.WriteAllText(Path.Combine(outDir, "Errors", "ErrorCode.cs"), SpecCodegen.RenderErrorCode(SpecCodegen.ErrorCodes(spec)));
            File.WriteAllText(Path.Combine(outDir, "Security", "PublicOperations.cs"), SpecCodegen.RenderPublicOperations(SpecCodegen.PublicOperations(spec)));
        }

        return 0;
    case ["dispatch-plan", .. var rest]:
        return DispatchPlan.Run(rest, Console.Out, Console.Error);
    default:
        Console.Error.WriteLine("Usage: codegen <spec> <outDir> | dispatch-plan <tag> <level> <url> <source> [<open>]");

        return 1;
}
