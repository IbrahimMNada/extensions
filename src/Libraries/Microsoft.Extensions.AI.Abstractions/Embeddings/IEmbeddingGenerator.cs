﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.AI;

/// <summary>Represents a generator of embeddings.</summary>
/// <typeparam name="TInput">The type from which embeddings will be generated.</typeparam>
/// <typeparam name="TEmbedding">The type of embeddings to generate.</typeparam>
/// <remarks>
/// <para>
/// Unless otherwise specified, all members of <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> are thread-safe for concurrent use.
/// It is expected that all implementations of <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> support being used by multiple requests concurrently.
/// Instances must not be disposed of while the instance is still in use.
/// </para>
/// <para>
/// However, implementations of <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> may mutate the arguments supplied to
/// <see cref="GenerateAsync"/>, such as by configuring the options instance. Thus, consumers of the interface either should
/// avoid using shared instances of these arguments for concurrent invocations or should otherwise ensure by construction that
/// no <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> instances are used which might employ such mutation.
/// </para>
/// </remarks>
public interface IEmbeddingGenerator<in TInput, TEmbedding> : IDisposable
    where TEmbedding : Embedding
{
    /// <summary>Generates embeddings for each of the supplied <paramref name="values"/>.</summary>
    /// <param name="values">The sequence of values for which to generate embeddings.</param>
    /// <param name="options">The embedding generation options with which to configure the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The generated embeddings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    Task<GeneratedEmbeddings<TEmbedding>> GenerateAsync(
        IEnumerable<TInput> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Asks the <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> for an object of the specified type <paramref name="serviceType"/>.</summary>
    /// <param name="serviceType">The type of object being requested.</param>
    /// <param name="serviceKey">An optional key that can be used to help identify the target service.</param>
    /// <returns>The found object, otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="serviceType"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The purpose of this method is to allow for the retrieval of strongly typed services that might be provided by the
    /// <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>, including itself or any services it might be wrapping.
    /// For example, to access the <see cref="EmbeddingGeneratorMetadata"/> for the instance, <see cref="GetService"/> may
    /// be used to request it.
    /// </remarks>
    object? GetService(Type serviceType, object? serviceKey = null);
}
