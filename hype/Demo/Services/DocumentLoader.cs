using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HypeDemo.Models;

namespace HypeDemo.Services;

public static class DocumentLoader
{
    public static List<Document> LoadAndChunkProjectsData(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var projects = content.Split("# Project", StringSplitOptions.RemoveEmptyEntries);
        
        projects = projects.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

        var documents = new List<Document>();
        
        for (int i = 0; i < projects.Length; i++)
        {
            var project = projects[i];
            var projectContent = $"# Project{project}"; // Add back the heading

            // Extract project name from the first line
            var lines = projectContent.Split('\n');
            var firstLine = lines[0];
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
                    var sectionLines = sections[j].Split('\n');
                    sectionName = sectionLines[0];
                }

                // Create a document for each section
                var docId = $"project_{i}_section_{j}";
                var doc = new Document
                {
                    Id = docId,
                    Content = sectionContent.Trim(),
                    Metadata = new ChunkMetadata
                    {
                        Project = projectName,
                        Section = sectionName,
                        ProjectIndex = i,
                        SectionIndex = j
                    }
                };
                documents.Add(doc);
            }
        }

        return documents;
    }
}
