using HockeySim.Desktop.Game;

using Xunit;

namespace HockeySim.Desktop.Tests;

public sealed class NavigationHistoryTests
{
    [Fact]
    public void ANewHistoryHoldsOnlyItsFirstEntry()
    {
        var history = new NavigationHistory<string>("home");

        Assert.Equal("home", history.Current);
        Assert.False(history.CanGoBack);
        Assert.False(history.CanGoForward);
        Assert.Throws<InvalidOperationException>(history.GoBack);
        Assert.Throws<InvalidOperationException>(history.GoForward);
    }

    [Fact]
    public void VisitingMovesOnAndKeepsTheEntryLeftBehind()
    {
        var history = new NavigationHistory<string>("home");

        history.Visit("roster");

        Assert.Equal("roster", history.Current);
        Assert.True(history.CanGoBack);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void VisitingTheCurrentEntryAddsNothing()
    {
        var history = new NavigationHistory<string>("home");

        history.Visit("home");

        Assert.False(history.CanGoBack);

        history.Visit("roster");
        history.Visit("roster");
        Assert.Equal("home", history.GoBack());
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void GoingBackAndForwardWalksTheEntriesInOrder()
    {
        var history = new NavigationHistory<string>("a");
        history.Visit("b");
        history.Visit("c");

        Assert.Equal("b", history.GoBack());
        Assert.Equal("a", history.GoBack());
        Assert.False(history.CanGoBack);
        Assert.True(history.CanGoForward);
        Assert.Equal("b", history.GoForward());
        Assert.Equal("c", history.GoForward());
        Assert.False(history.CanGoForward);
        Assert.Equal("c", history.Current);
        Assert.True(history.CanGoBack);
    }

    [Fact]
    public void VisitingAfterGoingBackDiscardsTheEntriesAhead()
    {
        var history = new NavigationHistory<string>("a");
        history.Visit("b");
        history.Visit("c");
        history.GoBack();

        history.Visit("d");

        Assert.False(history.CanGoForward);
        Assert.Equal("b", history.GoBack());
        Assert.Equal("a", history.GoBack());
    }

    [Fact]
    public void VisitingTheCurrentEntryAfterGoingBackKeepsTheEntriesAhead()
    {
        var history = new NavigationHistory<string>("a");
        history.Visit("b");
        history.GoBack();

        history.Visit("a");

        Assert.True(history.CanGoForward);
    }

    [Fact]
    public void ReplacingTheCurrentEntryChangesWhatBackAndForwardReturnTo()
    {
        var history = new NavigationHistory<string>("a");
        history.Visit("b");

        history.ReplaceCurrent("b2");

        Assert.Equal("b2", history.Current);
        Assert.Equal("a", history.GoBack());
        Assert.Equal("b2", history.GoForward());
    }

    [Fact]
    public void ReplacingTheCurrentEntryNeverAddsOne()
    {
        var history = new NavigationHistory<string>("a");

        history.ReplaceCurrent("b");

        Assert.False(history.CanGoBack);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void OnlyTheNewestEntriesBehindTheCurrentOneAreKept()
    {
        var history = new NavigationHistory<int>(0, capacity: 3);
        for (var entry = 1; entry <= 5; entry++)
        {
            history.Visit(entry);
        }

        Assert.Equal(4, history.GoBack());
        Assert.Equal(3, history.GoBack());
        Assert.Equal(2, history.GoBack());
        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void ACapacityOfZeroIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationHistory<int>(0, capacity: 0));
    }
}