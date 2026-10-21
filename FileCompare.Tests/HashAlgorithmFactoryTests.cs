using DuplicateAssistant.Business.FileHashing;

namespace FileCompare.Tests;

[TestFixture]
public class HashAlgorithmFactoryTests
{
    [TestCase("MD5", typeof(Md5FileHashing))]
    [TestCase("Blake3", typeof(Blake3FileHashing))]
    [TestCase("XXHash", typeof(XXHashFileHashing))]
    [TestCase("md5", typeof(Md5FileHashing))]
    [TestCase("blake3", typeof(Blake3FileHashing))]
    [TestCase("xxhash", typeof(XXHashFileHashing))]
    [TestCase("  Blake3  ", typeof(Blake3FileHashing))]
    [TestCase("", typeof(Blake3FileHashing))]
    [TestCase(null, typeof(Blake3FileHashing))]
    public void CreatesTheExpectedImplementation(string? name, Type expectedType)
    {
        Assert.That(HashAlgorithmFactory.Create(name), Is.InstanceOf(expectedType));
    }

    [Test]
    public void UnknownAlgorithmsAreRejected()
    {
        Assert.That(
            () => HashAlgorithmFactory.Create("crc32"),
            Throws.ArgumentException.With.Message.Contain("crc32"));
    }
}
