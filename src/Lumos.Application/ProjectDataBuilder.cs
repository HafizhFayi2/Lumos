using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.Application;

/// <summary>
/// Builds ProjectData from the current editor state for serialization.
/// </summary>
public static class ProjectDataBuilder
{
    public static ProjectSerializer.ProjectData BuildFromState(EditorState state)
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
                        manifest.Entries.Add(new MediaEntry
                        {
                            OriginalPath = clip.MediaRef,
                            Name = System.IO.Path.GetFileName(clip.MediaRef),
                            Type = clip.MediaType,
                            ProjectRelativePath = $"media/{System.IO.Path.GetFileName(clip.MediaRef)}",
                        });
                    }
                }
            }
        }

        return new ProjectSerializer.ProjectData(project, manifest);
    }
}
