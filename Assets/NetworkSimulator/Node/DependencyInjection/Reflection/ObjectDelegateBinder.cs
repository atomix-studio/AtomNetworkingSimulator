using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.DependencyProvider
{
    /// <summary>
    /// Permet de créer un ensemble de délégué par reflection sur une classe afin de réinitialiser les valeurs par défaut au runtime.
    /// Utilisé dans le système de pooling des GlyphAlterations pour réinitialiser les runes
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObjectDelegateBinder<T>
    {
        protected List<MemberDelegateBinder<T>> memberDelegateBinders = new List<MemberDelegateBinder<T>>();
        public List<MemberDelegateBinder<T>> MemberDelegateBinders => memberDelegateBinders;

        public ObjectDelegateBinder(T context)
        {
            var type = context.GetType();

            try
            {
                var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    if (!CheckAttributesValidity(field.CustomAttributes))
                        continue;

                    var delegateBinder = new MemberDelegateBinder<T>();
                    delegateBinder.BindedInstance = context;
                    delegateBinder.createFieldDelegatesAuto(field);
                    memberDelegateBinders.Add(delegateBinder);
                }

                var properties = type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                foreach (var property in properties)
                {
                    if (!CheckAttributesValidity(property.CustomAttributes))
                        continue;

                    var delegateBinder = new MemberDelegateBinder<T>();
                    delegateBinder.BindedInstance = context;
                    delegateBinder.createPropertyDelegatesAuto(property);
                    memberDelegateBinders.Add(delegateBinder);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        // todo attribute check as param
        protected virtual bool CheckAttributesValidity(IEnumerable<CustomAttributeData> customAttributes)
        {
            for (int i = 0; i < customAttributes.Count(); ++i)
            {
                if (customAttributes.ElementAt(i).AttributeType == typeof(InjectComponentAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Il est possible de forcer une autre instance dans la réinitialisation afin de ne créer qu'une instance du réinitialiseur par type d'objet et de passer l'objet en référence.
        /// </summary>
        /// <param name="instance"></param>
        public void Reset(T instance = default(T))
        {
            foreach (var memberDelegateBinder in memberDelegateBinders)
            {
                memberDelegateBinder.resetValueToDefault(instance);
            }
        }
    }
}
