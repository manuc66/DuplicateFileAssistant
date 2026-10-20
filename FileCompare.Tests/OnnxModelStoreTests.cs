using System.Net;
using System.Security.Cryptography;
using ImageCompare.Onnx;

namespace FileCompare.Tests;

[TestFixture]
public class OnnxModelStoreTests
{
    [Test]
    public async Task DownloadReportsProgressAndVerifiesSha256()
    {
        byte[] payload = CreatePayload(200_000);
        OnnxImageEmbeddingModel model = CreateModel(payload.Length, ComputeSha256(payload));
        RecordingProgress progress = new();
        using TempDirectory temp = new();
        using FakeHandler handler = new(payload);
        using HttpClient client = new(handler);
        OnnxModelStore store = new(temp.Root, client);

        IReadOnlyDictionary<string, string> files = await store.EnsureFilesAsync(
            model,
            allowDownload: true,
            log: null,
            CancellationToken.None,
            progress);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(files["model.bin"]), Is.True);
            Assert.That(progress.Values, Is.Not.Empty);
            Assert.That(progress.Values[0], Is.EqualTo(0));
            Assert.That(progress.Values[^1], Is.EqualTo(100));
            Assert.That(progress.Values.Any(value => value > 0 && value < 100), Is.True);
        });
    }

    [Test]
    public async Task DownloadRejectsAChangedPayload()
    {
        byte[] payload = CreatePayload(4096);
        OnnxImageEmbeddingModel model = CreateModel(payload.Length, new string('0', 64));
        using TempDirectory temp = new();
        using FakeHandler handler = new(payload);
        using HttpClient client = new(handler);
        OnnxModelStore store = new(temp.Root, client);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.EnsureFilesAsync(
            model,
            allowDownload: true,
            log: null,
            CancellationToken.None));

        Assert.That(File.Exists(Path.Combine(temp.Root, "fake", "model.bin")), Is.False);
    }

    [Test]
    public async Task MissingFileWithoutDownloadThrows()
    {
        using TempDirectory temp = new();
        using FakeHandler handler = new(CreatePayload(16));
        using HttpClient client = new(handler);
        OnnxModelStore store = new(temp.Root, client);
        OnnxImageEmbeddingModel model = CreateModel(16, new string('0', 64));

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.EnsureFilesAsync(
            model,
            allowDownload: false,
            log: null,
            CancellationToken.None));
    }

    private static OnnxImageEmbeddingModel CreateModel(long size, string sha256)
    {
        float[] channel = { 0.5f, 0.5f, 0.5f };

        return new OnnxImageEmbeddingModel(
            Id: "fake",
            DisplayName: "Fake",
            License: "MIT",
            OutputName: null,
            InputSize: 224,
            ResizeMode: ImageResizeMode.Stretch,
            Mean: channel,
            StandardDeviation: channel,
            Pooling: EmbeddingPooling.ClsToken,
            Dimensions: 3,
            Files: new[]
            {
                new OnnxModelFile(
                    "model.bin",
                    "https://example.test/model.bin",
                    sha256,
                    size)
            });
    }

    private static byte[] CreatePayload(int length)
    {
        byte[] payload = new byte[length];

        for (int index = 0; index < payload.Length; index++)
        {
            payload[index] = (byte)(index * 31);
        }

        return payload;
    }

    private static string ComputeSha256(byte[] payload)
    {
        return Convert.ToHexStringLower(SHA256.HashData(payload));
    }

    private sealed class RecordingProgress : IProgress<double>
    {
        public List<double> Values { get; } = new();

        public void Report(double value)
        {
            Values.Add(value);
        }
    }

    private sealed class FakeHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            });
        }
    }
}
