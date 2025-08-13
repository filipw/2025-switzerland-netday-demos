using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HydeDemo.Models;

namespace HydeDemo.Services;

public static class DocumentLoader
{
    public static List<Document> LoadAndChunkProjectsData(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var projects = content.Split("# Project", StringSplitOptions.RemoveEmptyEntries)[1..]; // Skip the first empty element

        var documents = new List<Document>();
        
        for (int i = 0; i < projects.Length; i++)
        {
            var project = projects[i];
            var projectContent = $"# Project{project}";

            // Extract project name from the first line
            var firstLine = projectContent.Split('\n')[0];
            var projectName = firstLine.Replace("# Project ", "").Split(" (")[0];

            // Split each project into sections
            var sections = projectContent.Split("\n## ", StringSplitOptions.RemoveEmptyEntries);

            for (int j = 0; j < sections.Length; j++)
            {
                string sectionContent;
                string sectionName;

                if (j == 0)
                {
                    sectionContent = sections[j];
                    sectionName = "Overview";
                }
                else
                {
                    sectionContent = $"## {sections[j]}";
                    sectionName = sections[j].Split('\n')[0];
                }

                // Create a document for each section
                var docId = $"project_{i}_section_{j}";
                var doc = new Document
                {
                    Id = docId,
                    Content = sectionContent.Trim(),
                    Metadata = new Dictionary<string, object>
                    {
                        ["project"] = projectName,
                        ["section"] = sectionName,
                        ["project_index"] = i,
                        ["section_index"] = j
                    }
                };
                documents.Add(doc);
            }
        }

        return documents;
    }
}
