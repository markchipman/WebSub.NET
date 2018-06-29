﻿using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebSub.AspNetCore.WebHooks.Receivers.Subscriber
{
    /// <summary>
    /// An interface representing delivered content.
    /// </summary>
    public interface IWebSubContent
    {
        /// <summary>
        /// Gets the Content-Type.
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// Reads content as a <see cref="byte"/> array.
        /// </summary>
        /// <returns>Content as a <see cref="byte"/> array.</returns>
        Task<byte[]> ReadAsBytesAsync();

        /// <summary>
        /// Reads content as a <see cref="string"/> instance.
        /// </summary>
        /// <returns>Content as a <see cref="string"/> instance.</returns>
        Task<string> ReadAsStringAsync(Encoding encoding = null);

        /// <summary>
        /// Reads content as an <see cref="IFormCollection"/> instance.
        /// </summary>
        /// <returns>Content as an <see cref="IFormCollection"/> instance.</returns>
        Task<IFormCollection> ReadAsFormDataAsync();

        /// <summary>
        /// Reads content as a <typeparamref name="TModel"/> instance.
        /// </summary>
        /// <typeparam name="TModel">The type of data to return.</typeparam>
        /// <returns>Content as a <typeparamref name="TModel"/> instance.</returns>
        Task<TModel> ReadAsModelAsync<TModel>();
    }
}
