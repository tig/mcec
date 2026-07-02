using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Commands;

/// <summary>
/// Regression guard for issue #200. <c>SerializedCommands.Deserialize</c> pipes every .commands file
/// through an XSLT that lower-cases ALL element and attribute names before XmlSerializer binds. Any
/// [XmlAttribute]/[XmlElement]/[XmlArray]/[XmlArrayItem] name containing an uppercase character is
/// written by SaveCommands but can never bind on load — the value is silently lost (this trap claimed
/// className/maxDepth/maxNodes/durationMs/maxWidth/workingDirectory across five commands). This test
/// walks every serialized Command type (and every nested serialized type reachable from them) and
/// asserts every XML name is fully lowercase, closing the bug class permanently.
/// </summary>
public class XmlNameCasingTests
{
    [Fact]
    public void AllSerializedXmlNames_AreLowercase()
    {
        List<string> offenders = [];
        foreach (Type type in CollectSerializedTypes()) {
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance)) {
                if (member is not PropertyInfo and not FieldInfo) {
                    continue;
                }
                foreach ((string source, string name) in EffectiveXmlNames(member)) {
                    if (name.Any(char.IsUpper)) {
                        offenders.Add($"{type.Name}.{member.Name}: [{source}] name \"{name}\" is not all-lowercase — " +
                            "the .commands lower-casing XSLT means this value will be silently dropped on load");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Serialized XML names must be all-lowercase (see SerializedCommands.Deserialize / ClickCommand note):\n"
            + string.Join("\n", offenders.Distinct()));
    }

    /// <summary>
    /// Every concrete Command subclass, the Command base, SerializedCommands (the document root), and —
    /// transitively — any type in the product assembly referenced by a serialized member (nested
    /// serialized types such as EmbeddedCommands' item types).
    /// </summary>
    private static HashSet<Type> CollectSerializedTypes()
    {
        Assembly assembly = typeof(Command).Assembly;
        Queue<Type> pending = new();
        HashSet<Type> visited = [];

        pending.Enqueue(typeof(SerializedCommands));
        foreach (Type t in assembly.GetTypes().Where(t => t.IsClass && typeof(Command).IsAssignableFrom(t))) {
            pending.Enqueue(t);
        }

        while (pending.Count > 0) {
            Type type = pending.Dequeue();
            if (!visited.Add(type)) {
                continue;
            }
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance)) {
                if (member is not PropertyInfo and not FieldInfo) {
                    continue;
                }
                if (member.GetCustomAttribute<XmlIgnoreAttribute>() is not null) {
                    continue;
                }
                // Types named by polymorphic [XmlElement(type)]/[XmlArrayItem(type)] entries.
                foreach (XmlElementAttribute el in member.GetCustomAttributes<XmlElementAttribute>()) {
                    Enqueue(el.Type);
                }
                foreach (XmlArrayItemAttribute item in member.GetCustomAttributes<XmlArrayItemAttribute>()) {
                    Enqueue(item.Type);
                }
                // The member's own (or element) type when it lives in the product assembly.
                Type memberType = member is PropertyInfo p ? p.PropertyType : ((FieldInfo)member).FieldType;
                Enqueue(memberType.IsArray ? memberType.GetElementType() : memberType);
                if (memberType.IsGenericType) {
                    foreach (Type arg in memberType.GetGenericArguments()) {
                        Enqueue(arg);
                    }
                }
            }
        }
        return visited;

        void Enqueue(Type? t)
        {
            if (t is not null && t.Assembly == assembly && t.IsClass && !visited.Contains(t)) {
                pending.Enqueue(t);
            }
        }
    }

    /// <summary>
    /// Yields the XML names XmlSerializer will actually use for a member: explicit names as written, and
    /// the member-name fallback when an attribute gives no name (a PascalCase member name is then just as
    /// broken as an explicit camelCase one). Un-named polymorphic entries for abstract types are skipped —
    /// an abstract type is never instantiated, so no element is ever emitted under that name.
    /// </summary>
    private static IEnumerable<(string Source, string Name)> EffectiveXmlNames(MemberInfo member)
    {
        if (member.GetCustomAttribute<XmlIgnoreAttribute>() is not null) {
            yield break;
        }
        foreach (XmlAttributeAttribute attr in member.GetCustomAttributes<XmlAttributeAttribute>()) {
            yield return ("XmlAttribute", string.IsNullOrEmpty(attr.AttributeName) ? member.Name : attr.AttributeName);
        }
        foreach (XmlElementAttribute el in member.GetCustomAttributes<XmlElementAttribute>()) {
            if (string.IsNullOrEmpty(el.ElementName) && el.Type?.IsAbstract == true) {
                continue; // catch-all like [XmlElement(typeof(Command))] — never serialized directly
            }
            yield return ("XmlElement", string.IsNullOrEmpty(el.ElementName) ? member.Name : el.ElementName);
        }
        foreach (XmlArrayAttribute arr in member.GetCustomAttributes<XmlArrayAttribute>()) {
            yield return ("XmlArray", string.IsNullOrEmpty(arr.ElementName) ? member.Name : arr.ElementName);
        }
        foreach (XmlArrayItemAttribute item in member.GetCustomAttributes<XmlArrayItemAttribute>()) {
            if (string.IsNullOrEmpty(item.ElementName) && item.Type?.IsAbstract == true) {
                continue; // catch-all like [XmlArrayItem(typeof(Command))]
            }
            yield return ("XmlArrayItem", string.IsNullOrEmpty(item.ElementName) ? (item.Type?.Name ?? member.Name) : item.ElementName);
        }
    }
}
