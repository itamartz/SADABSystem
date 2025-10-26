using System;
using System.Management;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class ComputerSystemInventory
{
    public static string GetAllComputerSystemProperties(bool prettyPrint = true)
    {
        var computerSystemProperties = new Dictionary<string, object>();

        if (OperatingSystem.IsWindows())
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
        {
            foreach (ManagementObject obj in searcher.Get())
            {
                foreach (PropertyData prop in obj.Properties)
                {
                    try
                    {
                        if (prop.IsArray && prop.Value != null)
                        {
                            var array = (Array)prop.Value;
                            var list = new List<object>();
                            foreach (var item in array)
                            {
                                list.Add(item);
                            }
                            computerSystemProperties[prop.Name] = list;
                        }
                        else
                        {
                            computerSystemProperties[prop.Name] = prop.Value ?? "N/A";
                        }
                    }
                    catch (Exception ex)
                    {
                        computerSystemProperties[prop.Name] = $"Error: {ex.Message}";
                    }
                }
                break;
            }
        }

        // Convert to JSON
        var options = new JsonSerializerOptions
        {
            WriteIndented = prettyPrint,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        return JsonSerializer.Serialize(computerSystemProperties, options);
    }

    public static void SaveToJsonFile(string filePath, bool prettyPrint = true)
    {
        var json = GetAllComputerSystemProperties(prettyPrint);
        System.IO.File.WriteAllText(filePath, json);
    }
}