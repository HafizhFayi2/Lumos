using System;
using System.Collections.Generic;
using System.Linq;
using Lumos.Application;
using Xunit;

namespace Lumos.Tests.Application;

public class MediaFolderStoreTests
{
    // ── Construction & seeding ──────────────────────────────────────────────

    [Fact]
    public void Constructor_SeedsDefaultFolders()
    {
        var store = new MediaFolderStore();
        var all = store.GetAll().ToList();
        Assert.Contains(all, f => f.Id == "imports");
        Assert.Contains(all, f => f.Id == "generated");
        Assert.Contains(all, f => f.Id == "music");
        Assert.Equal(3, all.Count);
    }

    // ── TryAdd ──────────────────────────────────────────────────────────────

    [Fact]
    public void TryAdd_AddsNewFolder()
    {
        var store = new MediaFolderStore();
        var folder = new MediaFolderData("custom", "Custom", null);
        Assert.True(store.TryAdd(folder));

        Assert.True(store.TryGet("custom", out var retrieved));
        Assert.NotNull(retrieved);
        Assert.Equal("Custom", retrieved!.Name);
    }

    [Fact]
    public void TryAdd_DuplicateId_ReturnsFalse()
    {
        var store = new MediaFolderStore();
        var folder = new MediaFolderData("test", "Test", null);
        Assert.True(store.TryAdd(folder));
        Assert.False(store.TryAdd(folder)); // same id
    }

    [Fact]
    public void TryAdd_WithParentId_StoresParentRelation()
    {
        var store = new MediaFolderStore();
        var child = new MediaFolderData("sub", "Sub", "imports");
        Assert.True(store.TryAdd(child));

        Assert.True(store.TryGet("sub", out var retrieved));
        Assert.Equal("imports", retrieved!.ParentId);
    }

    // ── TryGet ──────────────────────────────────────────────────────────────

    [Fact]
    public void TryGet_ExistingFolder_ReturnsTrue()
    {
        var store = new MediaFolderStore();
        Assert.True(store.TryGet("imports", out var folder));
        Assert.NotNull(folder);
        Assert.Equal("Imports", folder!.Name);
    }

    [Fact]
    public void TryGet_MissingFolder_ReturnsFalse()
    {
        var store = new MediaFolderStore();
        Assert.False(store.TryGet("nonexistent", out var folder));
        Assert.Null(folder);
    }

    // ── ContainsKey ─────────────────────────────────────────────────────────

    [Fact]
    public void ContainsKey_ExistingFolder_ReturnsTrue()
    {
        var store = new MediaFolderStore();
        Assert.True(store.ContainsKey("music"));
    }

    [Fact]
    public void ContainsKey_MissingFolder_ReturnsFalse()
    {
        var store = new MediaFolderStore();
        Assert.False(store.ContainsKey("does_not_exist"));
    }

    // ── TryUpdate ───────────────────────────────────────────────────────────

    [Fact]
    public void TryUpdate_UpdatesFolderName()
    {
        var store = new MediaFolderStore();
        Assert.True(store.TryGet("imports", out var original));
        Assert.NotNull(original);

        var updated = original! with { Name = "My Imports" };
        Assert.True(store.TryUpdate("imports", updated, original));

        Assert.True(store.TryGet("imports", out var retrieved));
        Assert.Equal("My Imports", retrieved!.Name);
    }

    [Fact]
    public void TryUpdate_StaleComparison_ReturnsFalse()
    {
        var store = new MediaFolderStore();
        // Attempt to update with a stale reference that doesn't match current value
        var stale = new MediaFolderData("imports", "Stale", null);
        var updated = stale with { Name = "Never Applied" };
        Assert.False(store.TryUpdate("imports", updated, stale));
    }

    // ── TryRemove ───────────────────────────────────────────────────────────

    [Fact]
    public void TryRemove_RemovesFolderAndReturnsIt()
    {
        var store = new MediaFolderStore();
        var folder = new MediaFolderData("temp", "Temp", null);
        Assert.True(store.TryAdd(folder));
        Assert.True(store.ContainsKey("temp"));

        Assert.True(store.TryRemove("temp", out var removed));
        Assert.NotNull(removed);
        Assert.Equal("Temp", removed!.Name);
        Assert.False(store.ContainsKey("temp"));
    }

    [Fact]
    public void TryRemove_MissingFolder_ReturnsFalse()
    {
        var store = new MediaFolderStore();
        Assert.False(store.TryRemove("nonexistent", out var removed));
        Assert.Null(removed);
    }

    [Fact]
    public void TryRemove_DefaultFolder_RemovesSuccessfully()
    {
        var store = new MediaFolderStore();
        Assert.True(store.TryRemove("imports", out _));
        Assert.False(store.ContainsKey("imports"));
        Assert.Equal(2, store.GetAll().Count());
    }

    // ── GetAll ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsAllFolders()
    {
        var store = new MediaFolderStore();
        var custom = new MediaFolderData("a", "A", null);
        var sub = new MediaFolderData("b", "B", "a");
        store.TryAdd(custom);
        store.TryAdd(sub);

        var all = store.GetAll().ToList();
        Assert.Contains(all, f => f.Id == "a");
        Assert.Contains(all, f => f.Id == "b");
        Assert.Contains(all, f => f.Id == "imports");
        Assert.Equal(5, all.Count);
    }

    // ── GetChildren ─────────────────────────────────────────────────────────

    [Fact]
    public void GetChildren_ReturnsDirectChildrenOnly()
    {
        var store = new MediaFolderStore();
        store.TryAdd(new MediaFolderData("parent", "Parent", null));
        store.TryAdd(new MediaFolderData("child1", "Child 1", "parent"));
        store.TryAdd(new MediaFolderData("child2", "Child 2", "parent"));
        store.TryAdd(new MediaFolderData("grandchild", "Grandchild", "child1"));

        var children = store.GetChildren("parent").ToList();
        Assert.Equal(2, children.Count);
        Assert.Contains(children, f => f.Id == "child1");
        Assert.Contains(children, f => f.Id == "child2");
        Assert.DoesNotContain(children, f => f.Id == "grandchild");
    }

    [Fact]
    public void GetChildren_NoChildren_ReturnsEmpty()
    {
        var store = new MediaFolderStore();
        var children = store.GetChildren("nonexistent").ToList();
        Assert.Empty(children);
    }

    // ── Clear ───────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_ResetsToDefaultFolders()
    {
        var store = new MediaFolderStore();
        store.TryAdd(new MediaFolderData("custom", "Custom", null));
        store.TryAdd(new MediaFolderData("extra", "Extra", null));
        Assert.Equal(5, store.GetAll().Count());

        store.Clear();

        var all = store.GetAll().ToList();
        Assert.Equal(3, all.Count);
        Assert.Contains(all, f => f.Id == "imports");
        Assert.Contains(all, f => f.Id == "generated");
        Assert.Contains(all, f => f.Id == "music");
        Assert.DoesNotContain(all, f => f.Id == "custom");
    }

    [Fact]
    public void Clear_EmptyStore_StillSeedsDefaults()
    {
        // Create a store, remove everything, then clear should still re-seed
        var store = new MediaFolderStore();
        foreach (var f in store.GetAll().ToList())
            store.TryRemove(f.Id, out _);
        Assert.Empty(store.GetAll());

        store.Clear();
        Assert.Equal(3, store.GetAll().Count());
    }

    // ── ReplaceAll ──────────────────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_ReplacesWithNewFolders()
    {
        var store = new MediaFolderStore();
        var replacements = new[]
        {
            new MediaFolderData("videos", "Videos", null),
            new MediaFolderData("audio", "Audio", null),
        };

        store.ReplaceAll(replacements);

        var all = store.GetAll().ToList();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, f => f.Id == "videos");
        Assert.Contains(all, f => f.Id == "audio");
        Assert.DoesNotContain(all, f => f.Id == "imports"); // defaults gone
    }

    [Fact]
    public void ReplaceAll_EmptyList_LeavesStoreEmpty()
    {
        var store = new MediaFolderStore();
        store.ReplaceAll(Array.Empty<MediaFolderData>());

        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void ReplaceAll_RetainsParentRelations()
    {
        var store = new MediaFolderStore();
        var replacements = new[]
        {
            new MediaFolderData("root", "Root", null),
            new MediaFolderData("child", "Child", "root"),
        };

        store.ReplaceAll(replacements);

        Assert.True(store.TryGet("child", out var child));
        Assert.Equal("root", child!.ParentId);
        Assert.Equal(2, store.GetAll().Count());
    }

    // ── BFS subfolder cleanup logic ─────────────────────────────────────────
    // The BFS algorithm lives in MediaFolderTools.DeleteMediaFolderAsync.
    // These tests verify the algorithmic correctness by re-implementing the
    // same traversal against the MediaFolderStore.

    [Fact]
    public void BfsCollectDescendants_FlatHierarchy_ReturnsOnlySelf()
    {
        var store = new MediaFolderStore();
        var folder = new MediaFolderData("solo", "Solo", null);
        store.TryAdd(folder);

        var collected = BfsCollect(store, "solo");
        Assert.Single(collected);
        Assert.Contains("solo", collected);
    }

    [Fact]
    public void BfsCollectDescendants_ParentAndChild_CollectsBoth()
    {
        var store = new MediaFolderStore();
        store.TryAdd(new MediaFolderData("parent", "Parent", null));
        store.TryAdd(new MediaFolderData("child", "Child", "parent"));

        var collected = BfsCollect(store, "parent");
        Assert.Equal(2, collected.Count);
        Assert.Contains("parent", collected);
        Assert.Contains("child", collected);
    }

    [Fact]
    public void BfsCollectDescendants_DeepNesting_CollectsAllInSubtree()
    {
        var store = new MediaFolderStore();
        store.ReplaceAll(Array.Empty<MediaFolderData>()); // start clean

        store.TryAdd(new MediaFolderData("a", "A", null));
        store.TryAdd(new MediaFolderData("b", "B", "a"));
        store.TryAdd(new MediaFolderData("c", "C", "b"));
        store.TryAdd(new MediaFolderData("d", "D", "c"));
        store.TryAdd(new MediaFolderData("e", "E", "c")); // sibling of d

        var collected = BfsCollect(store, "a");
        Assert.Equal(5, collected.Count);
        Assert.Contains("a", collected);
        Assert.Contains("b", collected);
        Assert.Contains("c", collected);
        Assert.Contains("d", collected);
        Assert.Contains("e", collected);
    }

    [Fact]
    public void BfsCollectDescendants_MultipleRoots_OnlyCollectsTargetSubtree()
    {
        var store = new MediaFolderStore();
        store.ReplaceAll(Array.Empty<MediaFolderData>()); // start clean

        store.TryAdd(new MediaFolderData("root1", "Root 1", null));
        store.TryAdd(new MediaFolderData("child1a", "Child 1A", "root1"));
        store.TryAdd(new MediaFolderData("root2", "Root 2", null));
        store.TryAdd(new MediaFolderData("child2a", "Child 2A", "root2"));

        var collected = BfsCollect(store, "root1");
        Assert.Equal(2, collected.Count);
        Assert.Contains("root1", collected);
        Assert.Contains("child1a", collected);
        Assert.DoesNotContain("root2", collected);
        Assert.DoesNotContain("child2a", collected);
    }

    [Fact]
    public void BfsCollectDescendants_SiblingOfParent_NotIncluded()
    {
        var store = new MediaFolderStore();
        store.ReplaceAll(Array.Empty<MediaFolderData>()); // start clean

        store.TryAdd(new MediaFolderData("root", "Root", null));
        store.TryAdd(new MediaFolderData("child", "Child", "root"));
        store.TryAdd(new MediaFolderData("orphan", "Orphan", null)); // sibling of root, not included

        var collected = BfsCollect(store, "root");
        Assert.Equal(2, collected.Count);
        Assert.DoesNotContain("orphan", collected);
    }

    // ── BFS traversal helper (mirrors MediaFolderTools.DeleteMediaFolderAsync) ─

    /// <summary>
    /// Replicates the BFS descendant collection from MediaFolderTools.DeleteMediaFolderAsync
    /// to independently verify the algorithm's correctness.
    /// </summary>
    private static HashSet<string> BfsCollect(MediaFolderStore store, string rootId)
    {
        var toRemove = new HashSet<string> { rootId };
        var queue = new Queue<string>([rootId]);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var f in store.GetAll())
            {
                if (f.ParentId == current && toRemove.Add(f.Id))
                    queue.Enqueue(f.Id);
            }
        }

        return toRemove;
    }
}
