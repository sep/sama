using Azure;
using Azure.Messaging.EventGrid;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace sama.Services;

  /// <summary>
  /// This is a wrapper around EventGridPublisherClient functionality that cannot be (easily) tested.
  /// </summary>
  [ExcludeFromCodeCoverage]
  public class EventGridPublisherClientWrapper
  {
      public virtual async Task SendEventAsync(Uri topicEndpoint, string accessKey, EventGridEvent eventGridEvent)
      {
          if (topicEndpoint == null)
          {
              throw new ArgumentNullException(nameof(topicEndpoint), "Topic endpoint is not configured");
          }

          if (string.IsNullOrWhiteSpace(accessKey))
          {
              throw new ArgumentNullException(nameof(accessKey), "Access key is not configured");
          }

          var credential = new AzureKeyCredential(accessKey);
          var client = new EventGridPublisherClient(topicEndpoint, credential);

          await client.SendEventAsync(eventGridEvent);
      }
  }
