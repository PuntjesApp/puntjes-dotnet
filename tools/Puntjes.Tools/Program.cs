using System.Text.Json;
using Puntjes.Tools;

switch (args)
{
    case ["codegen", var specPath, var outDir]:
        using (var spec = JsonDocument.Parse(File.ReadAllText(specPath)))
        {
            var numbersPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(specPath))!, "error-code-numbers.json");
            var numbers = SpecCodegen.ParseNumbers(File.ReadAllText(numbersPath));
            var codes = SpecCodegen.Numbered(SpecCodegen.ErrorCodes(spec), numbers);
            Directory.CreateDirectory(Path.Combine(outDir, "Errors"));
            Directory.CreateDirectory(Path.Combine(outDir, "Security"));
            File.WriteAllText(numbersPath, SpecCodegen.RenderNumbers(numbers));
            File.WriteAllText(Path.Combine(outDir, "Errors", "ErrorCode.cs"), SpecCodegen.RenderErrorCode(codes));
            File.WriteAllText(Path.Combine(outDir, "Security", "PublicOperations.cs"), SpecCodegen.RenderPublicOperations(SpecCodegen.PublicOperations(spec)));
            OptionalBodies.Apply(spec, outDir);
        }

        return 0;
    case ["dispatch-plan", .. var rest]:
        return DispatchPlan.Run(rest, Console.Out, Console.Error);
    default:
        Console.Error.WriteLine("Usage: codegen <spec> <outDir> | dispatch-plan <tag> <level> <url> <source> [<open>]");

        return 1;
}
