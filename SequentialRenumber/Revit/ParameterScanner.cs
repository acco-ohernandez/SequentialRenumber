using SequentialRenumber.Core;

namespace SequentialRenumber.Revit
{
    /// <summary>
    /// Discovers eligible text parameters on the anchor element and re-resolves the chosen
    /// one on every picked element via the stored key (spec 7.2 and section 6).
    /// </summary>
    internal static class ParameterScanner
    {
        /// <summary>
        /// Eligible dropdown entries for an element: instance parameters (Element.Parameters
        /// never yields type parameters) that are string-typed, writable, and have a live
        /// definition. Sorted alphabetically by display name.
        /// </summary>
        public static List<TargetParameterKey> Scan(Element element)
        {
            var results = new List<TargetParameterKey>();

            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter.StorageType != StorageType.String) continue;
                if (parameter.IsReadOnly) continue;
                if (parameter.Definition == null) continue;

                results.Add(CreateKey(parameter));
            }

            return results
                .OrderBy(k => k.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Re-resolves the session's target parameter on a picked element. Returns null when
        /// the element does not carry the parameter or it is not writable there — the caller
        /// logs a Skipped record.
        /// </summary>
        public static Parameter Resolve(Element element, TargetParameterKey key)
        {
            Parameter parameter = null;

            switch (key.Kind)
            {
                case ParameterKeyKind.BuiltIn:
                    parameter = element.get_Parameter((BuiltInParameter)key.BuiltInId);
                    break;
                case ParameterKeyKind.Shared:
                    parameter = element.get_Parameter(key.SharedGuid);
                    break;
                case ParameterKeyKind.Project:
                    parameter = element.LookupParameter(key.Name);
                    break;
            }

            if (parameter == null) return null;
            if (parameter.StorageType != StorageType.String) return null;
            if (parameter.IsReadOnly) return null;
            if (parameter.Definition == null) return null;

            return parameter;
        }

        private static TargetParameterKey CreateKey(Parameter parameter)
        {
            string name = parameter.Definition.Name;
            var key = new TargetParameterKey
            {
                Name = name,
                DisplayName = name,
                CurrentValue = parameter.AsString() ?? string.Empty,
            };

            long idValue = RevitVersionCompat.GetValue(parameter.Id);
            if (idValue < 0)
            {
                key.Kind = ParameterKeyKind.BuiltIn;
                key.BuiltInId = idValue;
            }
            else if (parameter.IsShared)
            {
                key.Kind = ParameterKeyKind.Shared;
                key.SharedGuid = parameter.GUID;
            }
            else
            {
                key.Kind = ParameterKeyKind.Project;
            }

            return key;
        }
    }
}
