using System.Text.Json;
using OpenCrawler.Cli.Commands;
using OpenCrawler.Core.Infrastructure;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace OpenCrawler.Cli.Tests;

public class DownloadCommandTests : IAsyncLifetime
{
    private WireMockServer _server = null!;
    private string _tempStorage = null!;

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();
        _tempStorage = Path.Combine(Path.GetTempPath(), "oc-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempStorage);

        _server.Given(Request.Create().WithPath("/article").UsingGet())
               .RespondWith(Response.Create()
                   .WithHeader("Content-Type", "text/html; charset=utf-8")
                   .WithBody("""
                       <!doctype html><html><head><title>Hello World</title></head>
                       <body><article>
                         <h1>Hello</h1>
                         <p>Some content that is long enough to avoid being detected as a challenge page.
                         Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor.</p>
                         <img src="/pic.png" alt="pic" />
                       </article></body></html>
                       """));

        var png = new byte[]
        {
            0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,
            0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
            0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,
            0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
            0x89,0x00,0x00,0x00,0x0D,0x49,0x44,0x41,
            0x54,0x78,0x9C,0x63,0x00,0x01,0x00,0x00,
            0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,
            0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
            0x42,0x60,0x82
        };
        _server.Given(Request.Create().WithPath("/pic.png").UsingGet())
               .RespondWith(Response.Create()
                   .WithHeader("Content-Type", "image/png")
                   .WithBody(png));

        _server.Given(Request.Create().WithPath("/missing").UsingGet())
               .RespondWith(Response.Create().WithStatusCode(404));

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _server.Stop();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(100);

        if (Directory.Exists(_tempStorage))
        {
            try { Directory.Delete(_tempStorage, true); }
            catch (IOException) { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public async Task IT01_Download_WritesFiles_And_InsertsArticle()
    {
        var origOut = Console.Out;
        var origErr = Console.Error;
        var sw = new StringWriter();
        var errSw = new StringWriter();
        Console.SetOut(sw);
        Console.SetError(errSw);
        try
        {
            var exit = await DownloadCommand.RunAsync($"{_server.Url}/article", "TestCategory", _tempStorage, "fast");
            Assert.True(exit == 0, $"Exit {exit}, stderr:\n{errSw}");

            var json = sw.ToString().Trim();
            using var doc = JsonDocument.Parse(json);
            var folderPath = doc.RootElement.GetProperty("folderPath").GetString()!;

            Assert.True(File.Exists(Path.Combine(folderPath, "index.html")));
            Assert.True(File.Exists(Path.Combine(folderPath, "content.txt")));
            Assert.True(File.Exists(Path.Combine(folderPath, "meta.json")));
            Assert.True(Directory.Exists(Path.Combine(folderPath, "assets")));

            var dbPath = AppPaths.DbFilePath(_tempStorage);
            Assert.True(File.Exists(dbPath));
        }
        finally { Console.SetOut(origOut); Console.SetError(origErr); }
    }

    [Fact]
    public async Task IT02_Duplicate_Download_CreatesSecondArticle()
    {
        var origOut = Console.Out;
        try
        {
            Console.SetOut(new StringWriter());
            var first = await DownloadCommand.RunAsync($"{_server.Url}/article", "dup", _tempStorage, "fast");
            Assert.Equal(0, first);

            Console.SetOut(new StringWriter());
            var second = await DownloadCommand.RunAsync($"{_server.Url}/article", "dup", _tempStorage, "fast");
            Assert.Equal(0, second);

            var dupDir = Path.Combine(_tempStorage, "dup");
            var subdirs = Directory.GetDirectories(dupDir);
            Assert.Equal(2, subdirs.Length);
        }
        finally { Console.SetOut(origOut); }
    }

    [Fact]
    public async Task IT03_Missing_Url_Returns_NonZero()
    {
        var origErr = Console.Error;
        var origOut = Console.Out;
        try
        {
            Console.SetError(new StringWriter());
            Console.SetOut(new StringWriter());
            var exit = await DownloadCommand.RunAsync($"{_server.Url}/missing", "TestCategory", _tempStorage, "fast");
            Assert.NotEqual(0, exit);
        }
        finally
        {
            Console.SetError(origErr);
            Console.SetOut(origOut);
        }
    }
}
