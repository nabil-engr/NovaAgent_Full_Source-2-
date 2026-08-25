using System.Diagnostics;

namespace NovaAgent.Tests;

public class WhisperServiceStartupTests
{
    [Fact]
    public void WhisperServerStartArguments_ShouldNotIncludeUnsupportedNoColorFlag()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "whisper-server.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = "runtime/whisper"
        };

        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("ggml-base.bin");
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add("8178");
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add("auto");
        startInfo.ArgumentList.Add("-nth");
        startInfo.ArgumentList.Add("0.70");

        Assert.DoesNotContain("-nc", startInfo.ArgumentList);
        Assert.Contains("-nth", startInfo.ArgumentList);
        Assert.Contains("--port", startInfo.ArgumentList);
    }
}
