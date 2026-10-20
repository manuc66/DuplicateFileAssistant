using DuplicateAssistant.Business.FileHashing;

namespace FileCompare.Tests;

[TestFixture]
public class HashAlgorithmFactoryTests
{
    [TestCase("MD5", typeof(Md5FileHashing))]
    [TestCase("Blake3", typeof(Blake3FileHashing))]
    [TestCase("XXHash", typeof(XXHashFileHashing))]
    [TestCase(null, typeof(Blake3FileHashing))]
    public void CreatesTheExpectedImplementation(string? name, Type expectedType)
    {
        Assert.That(HashAlgorithmFactory.Create(name), Is.InstanceOf(expectedType));
    }
}
