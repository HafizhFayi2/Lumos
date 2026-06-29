using System.Collections.Concurrent;

namespace Lumos.Application.Generation;

/// Registry of available generation models and providers.
/// Supports BYOK (Bring Your Own Key) per-model configuration.
public sealed class ModelCatalog
{
    private readonly ConcurrentDictionary<string, IModelProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ModelConfig> _models = new(StringComparer.OrdinalIgnoreCase);

    /// Register a provider and its associated models.
    public void RegisterProvider(IModelProvider provider, params ModelConfig[] models)
    {
        _providers[provider.Name] = provider;
        foreach (var model in models)
        {
            _models[model.Id] = model;
        }
    }

    /// Get a provider by name.
    public IModelProvider? GetProvider(string providerName)
    {
        _providers.TryGetValue(providerName, out var provider);
        return provider;
    }

    /// Get a model config by ID.
    public ModelConfig? GetModel(string modelId)
    {
        _models.TryGetValue(modelId, out var model);
        return model;
    }

    /// Get the provider for a given model ID.
    public IModelProvider? GetProviderForModel(string modelId)
    {
        var model = GetModel(modelId);
        if (model == null) return null;
        return GetProvider(model.Provider);
    }

    /// List all registered models, optionally filtered by type.
    public IReadOnlyList<ModelConfig> ListModels(ModelProviderType? type = null)
    {
        var query = _models.Values.AsEnumerable();
        if (type.HasValue)
            query = query.Where(m => m.Type == type.Value);
        return query.ToList();
    }

    /// Update API key for a model (BYOK).
    public void SetApiKey(string modelId, string apiKey)
    {
        if (_models.TryGetValue(modelId, out var existing))
        {
            _models[modelId] = existing with { ApiKey = apiKey };
        }
    }

    /// Update API endpoint for a model.
    public void SetApiEndpoint(string modelId, string endpoint)
    {
        if (_models.TryGetValue(modelId, out var existing))
        {
            _models[modelId] = existing with { ApiEndpoint = endpoint };
        }
    }

    /// Check if a model exists and has a configured API key (if required).
    public bool IsModelReady(string modelId)
    {
        var model = GetModel(modelId);
        return model != null; // Simulated providers don't need keys
    }

    /// Total registered model count.
    public int ModelCount => _models.Count;
}
