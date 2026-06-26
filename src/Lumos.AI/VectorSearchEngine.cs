using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Lumos.AI;

public class VectorSearchEngine
{
    private InferenceSession? _session;
    
    // In a real implementation, we would load the SigLIP 2 ONNX model here
    // For now, we mock the embedding generation since we don't have the model file.
    
    public Task InitializeAsync(string modelPath)
    {
        try
        {
            if (System.IO.File.Exists(modelPath))
            {
                _session = new InferenceSession(modelPath);
            }
        }
        catch
        {
            // Fallback for mock execution
        }
        return Task.CompletedTask;
    }

    public float[] GetEmbedding(string text)
    {
        if (_session != null)
        {
            // Real inference logic would go here
            // e.g. tokenizer -> inputs -> session.Run -> output tensor -> float array
        }
        
        // Mock semantic embedding (just a random vector for now)
        var rand = new Random(text.GetHashCode());
        var vec = new float[512];
        double sumSq = 0;
        for (int i = 0; i < 512; i++)
        {
            vec[i] = (float)rand.NextDouble() - 0.5f;
            sumSq += vec[i] * vec[i];
        }
        
        float norm = (float)Math.Sqrt(sumSq);
        for (int i = 0; i < 512; i++) vec[i] /= norm;
        return vec;
    }

    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Vector dimensions must match");
        float dotProduct = 0;
        float normA = 0;
        float normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        if (normA == 0 || normB == 0) return 0;
        return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public List<(string AssetId, float Score)> Search(string query, Dictionary<string, float[]> assetEmbeddings, int topK = 10)
    {
        var queryEmbedding = GetEmbedding(query);
        
        var results = new List<(string, float)>();
        foreach (var kvp in assetEmbeddings)
        {
            float score = CosineSimilarity(queryEmbedding, kvp.Value);
            results.Add((kvp.Key, score));
        }
        
        return results.OrderByDescending(r => r.Item2).Take(topK).ToList();
    }
}
