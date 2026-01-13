namespace OsintBackend.GraphQL.Types
{
    /// <summary>
    /// Generic mutation response wrapper for consistent error handling and result reporting
    /// </summary>
    /// <typeparam name="T">The type of data being returned on success</typeparam>
    public class MutationResponse<T>
    {
        /// <summary>
        /// Whether the mutation succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The returned data on success (null on failure)
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// Success message
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Error message on failure
        /// </summary>
        public string? Error { get; set; }
    }
}
