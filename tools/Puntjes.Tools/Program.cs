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
    default:
        Console.Error.WriteLine("Usage: codegen <spec> <outDir>");

        return 1;
}
