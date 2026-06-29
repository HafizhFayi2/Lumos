using Lumos.Application.Assets;
using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.Application;

/// <summary>
/// Builds ProjectData from the current editor state for serialization.
/// </summary>
public static class ProjectDataBuilder
{
    public static ProjectSerializer.ProjectData BuildFromState(EditorState state, MediaFolderStore? folderStore = null, AssetManager? assets = null)
    {
        var project = new Project
        {
            Id = state.ProjectId,
            Name = state.ProjectName,
            ModifiedAt = DateTime.UtcNow,
        };

        var timeline = state.Timeline.Timeline;
        if (timeline != null)
        {
            project.FrameRate = timeline.Fps;
            project.Width = timeline.Width;
            project.Height = timeline.Height;
            project.Timelines = new List<Timeline> { timeline };
        }

        var manifest = new MediaManifest();

        if (timeline != null)
        {
            foreach (var track in timeline.Tracks)
            {
                foreach (var clip in track.Clips)
                {
                    if (!string.IsNullOrEmpty(clip.MediaRef) &&
                        manifest.Entries.All(e => e.OriginalPath != clip.MediaRef))
                    {
                        // Look up FolderId from the asset catalog
                        string? folderId = null;
                        if (assets != null && assets.Catalog.TryGetByPath(clip.MediaRef, out var asset) && asset != null)
                            folderId = asset.FolderId;

                        manifest.Entries.Add(new MediaEntry
                        {
                            OriginalPath = clip.MediaRef,
                            Name = System.IO.Path.GetFileName(clip.MediaRef),
                            Type = clip.MediaType,
                            ProjectRelativePath = $"media/{System.IO.Path.GetFileName(clip.MediaRef)}",
                            FolderId = folderId,
                        });
                    }
                }
            }
        }

        // Build folder manifest from the current folder store
        var folders = new FolderManifest();
        if (folderStore != null)
        {
            foreach (var folder in folderStore.GetAll())
            {
                folders.Folders.Add(new FolderEntry
                {
                    Id = folder.Id,
                    Name = folder.Name,
                    ParentId = folder.ParentId,
                });
            }
        }

        return new ProjectSerializer.ProjectData(project, manifest, folders);
    }

    /// Restore folders from a FolderManifest into the MediaFolderStore.
    /// Replaces all folders with those from the manifest (falling back to defaults if manifest is empty).
    public static void RestoreFolders(MediaFolderStore store, FolderManifest? manifest)
    {
        if (manifest == null || manifest.Folders.Count == 0)
        {
            store.Clear(); // keeps defaults
            return;
        }

        store.ReplaceAll(manifest.Folders.Select(e => new MediaFolderData(e.Id, e.Name, e.ParentId)));
    }
}
