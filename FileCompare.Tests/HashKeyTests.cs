namespace FileCompare.Tests;

[TestFixture]
public class HashKeyTests
{
    [Test]
    public void EqualBytesProduceEqualKeys()
    {
        HashKey first = new(new byte[] { 1, 2, 3 });
        HashKey second = new(new byte[] { 1, 2, 3 });

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        });
    }

    [Test]
    public void DiscriminatorMakesCollisionGroupsDistinct()
    {
        HashKey first = new(new byte[] { 1, 2, 3 });
        HashKey second = first.WithDiscriminator(1);

        Assert.Multiple(() =>
        {
            Assert.That(first.Equals(second), Is.False);
            Assert.That(first.WithDiscriminator(1), Is.EqualTo(second));
        });
    }
}
