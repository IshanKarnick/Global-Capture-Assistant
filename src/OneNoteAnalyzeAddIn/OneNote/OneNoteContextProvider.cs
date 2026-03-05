using System.Reflection;
using System.Xml.Linq;
using OneNoteAnalyzeAddIn.Diagnostics;
using OneNoteAnalyzeAddIn.Models;

namespace OneNoteAnalyzeAddIn.OneNote;

public sealed class OneNoteContextProvider
{
    private const int HierarchyScopeSelf = 0;
    private const int XmlSchemaCurrent = 2;

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
        var args3 = new object?[] { pageId, null, 0 };
        if (TryInvoke(_oneNoteApplication, "GetPageContent", args3))
        {
            return args3[1] as string;
        }

        var args2 = new object?[] { pageId, null };
        if (TryInvoke(_oneNoteApplication, "GetPageContent", args2))
        {
            return args2[1] as string;
        }

        return null;
    }

    private string? TryGetHierarchyXml(string pageId)
    {
        var args4 = new object?[] { pageId, HierarchyScopeSelf, null, XmlSchemaCurrent };
        if (TryInvoke(_oneNoteApplication, "GetHierarchy", args4))
        {
            return args4[2] as string;
        }

        var args3 = new object?[] { pageId, HierarchyScopeSelf, null };
        if (TryInvoke(_oneNoteApplication, "GetHierarchy", args3))
        {
            return args3[2] as string;
        }

        return null;
    }

    private static PageContext BuildContext(string pageId, string pageXml, string? hierarchyXml)
    {
        var pageDoc = XDocument.Parse(pageXml);
        var page = pageDoc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Page", StringComparison.OrdinalIgnoreCase));
        var title = page?.Attribute("name")?.Value ?? "Untitled";

        var sectionName = "Unknown section";
        var notebookName = "Unknown notebook";

        if (!string.IsNullOrWhiteSpace(hierarchyXml))
        {
            var hierarchyDoc = XDocument.Parse(hierarchyXml);
            sectionName = hierarchyDoc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("Section", StringComparison.OrdinalIgnoreCase))
                ?.Attribute("name")?.Value ?? sectionName;
            notebookName = hierarchyDoc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("Notebook", StringComparison.OrdinalIgnoreCase))
                ?.Attribute("name")?.Value ?? notebookName;
        }

        return new PageContext(pageId, title, sectionName, notebookName);
    }

    private static object? GetPropertyValue(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        return property?.GetValue(target);
    }

    private static bool TryInvoke(object target, string methodName, object?[] parameters)
    {
        try
        {
            _ = target.GetType().InvokeMember(methodName, BindingFlags.InvokeMethod, binder: null, target: target, args: parameters);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
