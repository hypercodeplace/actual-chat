using System.Diagnostics.CodeAnalysis;
using Stl.Time.Testing;

namespace ActualChat.Core.UnitTests.Channels;

public class AsyncEnumerableExtTest
{

    [Fact]
    public async Task BasicMergeTest()
    {
        var leftTcs1 = new TaskCompletionSource();
        var leftTcs2 = new TaskCompletionSource();
        var leftTcs3 = new TaskCompletionSource();
        var leftTcs4 = new TaskCompletionSource();
        var rightTcs1 = new TaskCompletionSource();
        var rightTcs2 = new TaskCompletionSource();
        var rightTcs3 = new TaskCompletionSource();
        var rightTcs4 = new TaskCompletionSource();

        var left = Left();
        var right = Right();
        var result = left.Merge(right);
        var resultList = await result.ToListAsync();
        resultList.Should().BeEquivalentTo(new[] { 0, 1, 2, 10, 3, 4, 5, 20, 6, 30 }, options => options.WithStrictOrdering());


        // ReSharper disable AccessToModifiedClosure
        async IAsyncEnumerable<int> Left()
        {
            yield return 0;

            rightTcs1.SetResult();
            await leftTcs1.Task;

            yield return 10;

            rightTcs2.SetResult();
            await leftTcs2.Task;
            rightTcs3.SetResult();
            await leftTcs3.Task;

            yield return 20;

            rightTcs4.SetResult();
            await leftTcs4.Task;

            yield return 30;
        }

        async IAsyncEnumerable<int> Right()
        {
            await rightTcs1.Task;

            yield return 1;
            yield return 2;

            leftTcs1.SetResult();
            await rightTcs2.Task;

            yield return 3;
            yield return 4;

            leftTcs2.SetResult();
            await rightTcs3.Task;

            yield return 5;

            leftTcs3.SetResult();
            await rightTcs4.Task;

            yield return 6;
            leftTcs4.SetResult();
        }
        // ReSharper enable AccessToModifiedClosure
    }


    [Fact]
    public async Task MergeCancellationTest()
    {
        var clock = new TestClock();
        var cts = new CancellationTokenSource();
        var left = Left(cts.Token);
        var right = Right(cts.Token);
        var result = left.Merge(right);
        await foreach (var n in result.TrimOnCancellation()) {
            if (n == 10)
                cts.Cancel();
        }

        async IAsyncEnumerable<int> Left([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return 0;

            await clock.Delay(100);

            yield return 10;

            await clock.Delay(100);

            yield return 20;

            cancellationToken.ThrowIfCancellationRequested();

            await clock.Delay(300);

            yield return 30;
        }

        async IAsyncEnumerable<int> Right([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await clock.Delay(20);

            yield return 1;
            yield return 2;

            await clock.Delay(150);

            yield return 3;
            yield return 4;

            await clock.Delay(10);

            yield return 5;

            await clock.Delay(100);

            cancellationToken.ThrowIfCancellationRequested();

            yield return 6;
        }
    }

    [Fact]
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public async Task RandomizedMergeTest()
    {
        var testClock = new TestClock();
        var cts = new CancellationTokenSource();
        var random = new Random();
        var otherSourceLength = random.Next(4, 7);
        var sequenceLength = 100;
        var source = GenerateRandomUniqueSequence(sequenceLength, testClock, random, cts.Token);
        var otherSources = Enumerable.Range(0, otherSourceLength)
            .Select(_ => GenerateRandomUniqueSequence(sequenceLength, testClock, random, cts.Token));

        cts.CancelAfter(30*1000);
        var otherSourceArray = otherSources.ToArray();
        var count = await source.Merge(otherSourceArray).CountAsync(cancellationToken: cts.Token);
        count.Should().Be(sequenceLength * (otherSourceLength + 1));

        var sum = await source.Merge(otherSourceArray).SumAsync(i => i, cancellationToken: cts.Token);
        sum.Should().Be((otherSourceLength + 1) * (sequenceLength - 1) * sequenceLength / 2);
    }

    private async IAsyncEnumerable<int> GenerateRandomUniqueSequence(
        int length,
        ITestClock delayClock,
        Random random,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var randomNumbers = Enumerable.Range(0, length)
            .Select(i => (i, random: random.Next()))
            .OrderBy(x => x.random)
            .Select(x => x.i);
        foreach (var number in randomNumbers) {
            await delayClock.Delay(random.Next(100), cancellationToken: cancellationToken).ConfigureAwait(false);
            yield return number;
        }
    }
}
