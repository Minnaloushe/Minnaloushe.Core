using AwesomeAssertions;
using Minnaloushe.Core.Toolbox.Collections;

namespace Minnaloushe.Core.Collections.Tests
{
    [TestFixture]
    public class ConcurrentCircularBufferTests
    {
        [Test]
        public void ConstructorWhenCapacityZeroThenThrows()
        {
            Action act1 = () => new ConcurrentCircularBuffer<int>(0);
            act1.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void ConstructorWhenCapacityNegativeThenThrows()
        {
            Action act2 = () => new ConcurrentCircularBuffer<int>(-1);
            act2.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void AddWhenItemsAddedThenCountIncreasesAndOrderMaintained()
        {
            var buf = new ConcurrentCircularBuffer<int>(5)
            {
                1, 2, 3
            };

            buf.Count.Should().Be(3);
            buf.Capacity.Should().Be(5);
            buf.ToArray().Should().Equal(1, 2, 3);
        }

        [Test]
        public void AddWhenCapacityExceededThenOverwritesOldest()
        {
#pragma warning disable IDE0028
            var buf = new ConcurrentCircularBuffer<int>(3);
#pragma warning restore IDE0028
            buf.Add(1);
            buf.Add(2);
            buf.Add(3);
            buf.Add(4); // should overwrite 1

            buf.Count.Should().Be(3);
            buf.ToArray().Should().Equal(new[] { 2, 3, 4 });
        }

        [Test]
        public void CopyToWhenCalledThenCopiesItemsWithOffset()
        {
            var buf = new ConcurrentCircularBuffer<int>(4)
            {
                10, 20, 30
            };

            var arr = new int[5]; // initialized to zeros
            buf.CopyTo(arr, 1);

            arr.Should().Equal(0, 10, 20, 30, 0);
        }

        [Test]
        public void ToArrayWhenNotFullThenReturnsOnlyAddedItems()
        {
            var buf = new ConcurrentCircularBuffer<int>(5)
            {
                42, 84
            };

            var arr = buf.ToArray();
            arr.Length.Should().Be(2);
            arr.Should().Equal(42, 84);
        }

        [Test]
        public void ClearWhenCalledThenEmptiesBuffer()
        {
            var buf = new ConcurrentCircularBuffer<int>(3)
            {
                1, 2
            };

            buf.Clear();

            buf.Count.Should().Be(0);
            buf.ToArray().Should().BeEmpty();
        }

        [Test]
        public void GetEnumeratorWhenCalledThenReturnsSnapshot()
        {
            var buf = new ConcurrentCircularBuffer<int>(3)
            {
                1, 2
            };

            var enumerator = buf.GetEnumerator();

            // modify after getting enumerator
            buf.Add(3);
            buf.Add(4);

            var items = new List<int>();
            while (enumerator.MoveNext())
            {
                items.Add(enumerator.Current);
            }

            // enumerator should reflect snapshot at time of call (1,2)
            items.Should().Equal(new[] { 1, 2 });
        }
    }
}
