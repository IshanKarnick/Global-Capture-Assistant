using System.Reflection;
using System.Xml.Linq;
using OneNoteAnalyzeAddIn.Diagnostics;
using OneNoteAnalyzeAddIn.Models;

namespace OneNoteAnalyzeAddIn.OneNote;

public sealed class OneNoteContextProvider
{
    private readonly object _oneNoteApplication;
    private readonly AppLogger _logger;

    public OneNoteContextProvider(object oneNoteApplication, AppLogger logger)
    {
        _oneNoteApplication = oneNoteApplication;
        _logger = logger;
    }

    public PageContext? TryGetActivePageContext(string? correlationId = null)
    {
        try
        {
            var pageId = TryGetCurrentPageId();
            if (string.IsNullOrWhiteSpace(pageId))
            {
                return null;
            }

            var pageXml = TryGetPageContent(pageId);
            if (string.IsNullOrWhiteSpace(pageXml))
            {
                return null;
            }

            return BuildContext(pageId, pageXml, TryGetHierarchyXml(pageId));
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not read OneNote page context: {ex.Message}", correlationId);
            return null;
        }
    }

    private string? TryGetCurrentPageId()
    {
        var windows = GetPropertyValue(_oneNoteApplication, "Windows");
        var currentWindow = windows is null ? null : GetPropertyValue(windows, "CurrentWindow");
        return currentWindow is null ? null : GetPropertyValue(currentWindow, "CurrentPageId")?.ToString();
    }

    private string? TryGetPageContent(string pageId)
    {
        // GetPageContent(pageId, out xml, piAll)
        var parameters = new object?[] { pageId, null, 0 };
        var result = InvokeMethod(_oneNoteApplication, "GetPageContent", parameters);
        return result as string ?? parameters[1] as string;
    }

    private string? TryGetHierarchyXml(string pageId)
    {
        // GetHierarchy(startNodeId, hsSelf, out xml, xsCurrent)
        var parameters = new object?[] { pageId, 0, null, 2 };
        var result = InvokeMethod(_oneNoteApplication, "GetHierarchy", parameters);
        return result as string ?? parameters[2] as string;
    }

    private static PageContext BuildContext(string pageId, string pageXml, string? hierarchyXml)
    {
        var pageDoc = XDocument.Parse(pageXml);
        var page = pageDoc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Page", StringComparison.OrdinalIgnoreCase));
        var title = page?.Attribute("name")?.Value ?? "Untitled";

        string sectionName = "Unknown section";
        string notebookName = "Unknown notebook";

        if (!string.IsNullOrWhiteSpace(hierarchyXml))
        {
            var hierarchyDoc = XDocument.Parse(hierarchyXml);
            sectionName = hierarchyDoc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Section", StringComparison.OrdinalIgnoreCase))?.Attribute("name")?.Value ?? sectionName;
            notebookName = hierarchyDoc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Notebook", StringComparison.OrdinalIgnoreCase))?.Attribute("name")?.Value ?? notebookName;
        }

        return new PageContext(pageId, title, sectionName, notebookName);
    }

    private static object? GetPropertyValue(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        return property?.GetValue(target);
    }

    private static object? InvokeMethod(object target, string methodName, object?[] parameters)
    {
        return target.GetType().InvokeMember(methodName, BindingFlags.InvokeMethod, binder: null, target: target, args: parameters);
    }
}
