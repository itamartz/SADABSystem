namespace SADAB.Shared.Extensions;

/// <summary>
/// Extension methods for object serialization and formatting.
/// </summary>
public static class ObjectExtensions
{
    /// <summary>
    /// Converts an object to a Key=Value string representation using reflection.
    /// Automatically handles sensitive fields, collections, and special types.
    /// </summary>
    /// <param name="obj">The object to convert to string.</param>
    /// <returns>A string in Key=Value format with all properties.</returns>
    /// <remarks>
    /// This method automatically:
    /// - Masks sensitive fields (Password, PrivateKey) with "***"
    /// - Truncates long strings (Token, Certificate) to 50 characters
    /// - Expands Dictionary collections to show key-value pairs
    /// - Formats DateTime objects as "yyyy-MM-dd HH:mm:ss"
    /// - Shows collection counts for List types
    /// - Handles null values gracefully
    /// </remarks>
    public static string ToKeyValueString(this object obj)
    {
        var properties = obj.GetType().GetProperties();
        var propertyValues = new List<string>();

        foreach (var prop in properties)
        {
            var value = prop.GetValue(obj);
            string formattedValue;

            if (value == null)
            {
                formattedValue = "null";
            }
            // Mask sensitive fields
            else if (prop.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                     prop.Name.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase))
            {
                formattedValue = "***";
            }
            // Truncate tokens
            else if (prop.Name.Equals("Token", StringComparison.OrdinalIgnoreCase) && value is string tokenStr)
            {
                formattedValue = $"{tokenStr.Substring(0, Math.Min(20, tokenStr.Length))}...";
            }
            // Truncate certificates
            else if (prop.Name.Contains("Certificate", StringComparison.OrdinalIgnoreCase) && value is string certStr)
            {
                formattedValue = $"{certStr.Substring(0, Math.Min(50, certStr.Length))}...";
            }
            // Truncate long output strings
            else if ((prop.Name.Equals("Output", StringComparison.OrdinalIgnoreCase) ||
                      prop.Name.Equals("ErrorOutput", StringComparison.OrdinalIgnoreCase) ||
                      prop.Name.Equals("ErrorMessage", StringComparison.OrdinalIgnoreCase)) &&
                     value is string outputStr && outputStr.Length > 50)
            {
                formattedValue = $"\"{outputStr.Substring(0, 50)}...\"";
            }
            // Expand Dictionary<string, object>
            else if (value is Dictionary<string, object> dictObj)
            {
                var items = dictObj.Select(kvp => $"{kvp.Key}={kvp.Value}");
                formattedValue = $"[{string.Join(", ", items)}]";
            }
            // Expand Dictionary<string, string>
            else if (value is Dictionary<string, string> dictStr)
            {
                var items = dictStr.Select(kvp => $"{kvp.Key}={kvp.Value}");
                formattedValue = $"[{string.Join(", ", items)}]";
            }
            // Show count for List<Guid>
            else if (value is List<Guid> guidList)
            {
                formattedValue = $"{guidList.Count} items";
            }
            // Show count for List<int> (SuccessExitCodes)
            else if (prop.Name.Contains("ExitCodes", StringComparison.OrdinalIgnoreCase) && value is List<int> intList)
            {
                formattedValue = $"[{string.Join(", ", intList)}]";
            }
            // Show count for List<string>
            else if (value is List<string> strList)
            {
                // If it's Files property, show count. Otherwise, show first few items
                if (prop.Name.Equals("Files", StringComparison.OrdinalIgnoreCase))
                {
                    formattedValue = $"{strList.Count} items";
                }
                else
                {
                    formattedValue = $"{strList.Count} items";
                }
            }
            // Show count for generic lists
            else if (value.GetType().IsGenericType &&
                     value.GetType().GetGenericTypeDefinition() == typeof(List<>))
            {
                var countProp = value.GetType().GetProperty("Count");
                var count = countProp?.GetValue(value);
                formattedValue = $"{count} items";
            }
            // Format DateTime
            else if (value is DateTime dt)
            {
                formattedValue = dt.ToString("yyyy-MM-dd HH:mm:ss");
            }
            // Default: use ToString()
            else
            {
                formattedValue = value.ToString() ?? "null";
            }

            propertyValues.Add($"{prop.Name}={formattedValue}");
        }

        return string.Join(", ", propertyValues);
    }
}
