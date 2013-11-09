// $Id: RegistryService.cs 1497 2007-11-26 21:18:36Z fred $

using System;
using System.Reflection;
using Microsoft.Win32;
using UW.ClassroomPresenter.Model.Viewer;
using UW.ClassroomPresenter.Model;

namespace UW.ClassroomPresenter.Misc {
    /// <summary>
    /// The class provides a service for storing various parameters in the registry
    ///
    /// TODO: Add some sanity checks to ensure that two different RegistryService classes can't
    ///       munge the same registry entries (i.e. be having two properties with the same name
    ///       from different objects
    /// TODO: Get the registry string from the application config file
    /// TODO: Is requiring "Parse" the best way or should be use the Convert class instead?
    ///       Perhaps allowing an attribute parameter specifying a static method to invoke
    ///       to convert is an alternative?
    /// </summary>
    public class RegistryService : IDisposable {

        #region Private Members

        /// <summary>
        /// True once the object is disposed, false otherwise.
        /// </summary>
        private bool m_bDisposed = false;

        /// <summary>
        /// The PublisherProperty class whoes properties we are saving
        /// </summary>
        private PropertyPublisher m_PropertyPublisher;

        /// <summary>
        /// The registry string where we're saving all properties
        /// </summary>
        public const string m_szRegistryString = "Software\\UW CSE\\Presenter\\V3";
        /// <summary>
        /// The registry key where we're saving all properties
        /// </summary>
        private RegistryKey m_RegistryKey;

        #endregion

        #region Initialization

        /// <summary>
        /// Constructs a service to keep track of changes to the model objects and store them in the
        /// registry
        /// </summary>
        /// <param name="toSave">The model class with values we want to save in the registry</param>
        public RegistryService( PropertyPublisher toSave ) {
            // Save the object we're serializing
            this.m_PropertyPublisher = toSave;

            // Get the registry key and if it doesn't exist the create it
            this.m_RegistryKey = Registry.CurrentUser.OpenSubKey( RegistryService.m_szRegistryString, true );
            if( this.m_RegistryKey == null ) {
                this.m_RegistryKey = Registry.CurrentUser.CreateSubKey( RegistryService.m_szRegistryString );
            }

            // Get all the properties that are supposed to be saved and add listeners to them
            foreach( PropertyInfo property in this.m_PropertyPublisher.GetType().GetProperties() ) {
                object[] publishAttribs = property.GetCustomAttributes( typeof(PropertyPublisher.PublishedAttribute), true );
                object[] savedAttribs = property.GetCustomAttributes( typeof(PropertyPublisher.SavedAttribute), true );

                // Check if the property should be saved
                if( savedAttribs.Length >= 1 ) {
                    // Initialize Values
                    string oldValue = this.GetStringRegistryValue( property.Name );
                    if( oldValue != null )
                        this.SetModelValue( property.Name, oldValue );

                    // Hook a handler to events that are published so they are saved on change
                    if( publishAttribs.Length >= 1 ) {
                        this.m_PropertyPublisher.Changed[property.Name].Add(new PropertyEventHandler(this.OnPropertyChanged));
                    }
                }
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Event handler that's invoked whenever a property changes that we are saving.
        /// This immediately exports the new value out to the registry.
        /// </summary>
        /// <param name="sender">The object that was changed</param>
        /// <param name="args">Information about the changed property</param>
        private void OnPropertyChanged( object sender, PropertyEventArgs args ) {
            // Get the value from the model
            object propValue = this.GetModelValue( args.Property );
            // Set the registry with the correct value
            if( propValue != null )
                this.SetStringRegistryValue( args.Property, propValue.ToString() );
        }

        #endregion

        #region Read/Write Values

        /// <summary>
        /// Saves all the properties flagged [Saved] out to the registry
        /// </summary>
        public void SaveAllProperties() {
            foreach( PropertyInfo property in this.m_PropertyPublisher.GetType().GetProperties() ) {
                object[] savedAttribs = property.GetCustomAttributes( typeof(PropertyPublisher.SavedAttribute), true );

                // Check if the property should be saved
                if( savedAttribs.Length >= 1 ) {
                    // Get the value from the model
                    object propValue = this.GetModelValue( property.Name );
                    // Set the registry with the correct value
                    if( propValue != null )
                        this.SetStringRegistryValue( property.Name, propValue.ToString() );
                }
            }
        }

        /// <summary>
        /// Get a value from the model
        /// </summary>
        /// <param name="name">The name of the property to get</param>
        /// <returns>The value of the property</returns>
        private object GetModelValue( string name ) {
            // Get the property info for this property
            PropertyInfo propInfo = this.m_PropertyPublisher.GetType().GetProperty( name );
            // Get the value of this property
            object result = null;
            using(Synchronizer.Lock(this.m_PropertyPublisher.SyncRoot)) {
                result = propInfo.GetValue( this.m_PropertyPublisher, null );
            }
            return result;
        }

        /// <summary>
        /// Set a value in the model, requires that the property have a Parse(string s) method
        /// </summary>
        /// <param name="name">The name of the property to set</param>
        /// <param name="val">The value to set the property to</param>
        private void SetModelValue( string name, string val ) {
            // Get the object property we want to modify
            PropertyInfo propInfo = this.m_PropertyPublisher.GetType().GetProperty( name );

            // Lock the object
            using(Synchronizer.Lock(this.m_PropertyPublisher.SyncRoot)) {
                // Get the object we want to change
                object oldValue = propInfo.GetValue( this.m_PropertyPublisher, null );
                // TODO: Is requiring "Parse" the best way or should be use the Convert class instead?
                System.Type oldValueType = oldValue.GetType();
                MethodInfo methodInfo = oldValueType.GetMethod( "Parse", new Type[] { typeof(string) });
                if( methodInfo != null ) {
                    try {
                        // Invoke the parsing method
                        object convertedValue = methodInfo.Invoke( oldValue, new object[] { val } );
                        // Write this value to the property
                        propInfo.SetValue( this.m_PropertyPublisher, convertedValue, null );
                    } catch( System.Reflection.TargetInvocationException ) {
                        // Do nothing, it should get the default value
                    }
                } else if( oldValue is string ) {
                    propInfo.SetValue( this.m_PropertyPublisher, val, null );
                }
                    // rja - not sure why this wasn't working for DockStyle - this fixes bug 1076
                else if (oldValue is System.Enum) {
                    try {
                        object convertedValue = Enum.Parse( oldValueType, val, true );
                        propInfo.SetValue( this.m_PropertyPublisher, convertedValue, null );
                    } catch( System.ArgumentNullException ) {
                        // Do Nothing, it should get the default value
                    } catch( System.ArgumentException ) {
                        // Do Nothing, it should get the default value
                    }
                }
            }
        }

        /// <summary>
        /// Returns a property that is of type string.
        /// </summary>
        /// <param name="name">The name of the property to return.</param>
        /// <returns>The string that corresponds to the specified key or null
        /// if no such key exists.</returns>
        protected string GetStringRegistryValue( string name ) {
            return RegistryService.GetStringRegistryValue( this.m_RegistryKey, name );
        }

        /// <summary>
        /// Returns a property that is of type string.
        /// </summary>
        /// <param name="key">The key to look in.</param>
        /// <param name="name">The name of the property to return.</param>
        /// <returns>The string that corresponds to the specified key or null
        /// if no such key exists.</returns>
        public static string GetStringRegistryValue( RegistryKey key, string name ) {
            object result = key.GetValue( name );
            if( result == null )
                return null;
            else
                return Convert.ToString( result );
        }

        /// <summary>
        /// Sets the value of a property with a string value.
        /// </summary>
        /// <param name="name">The name of the property whose value to set.</param>
        /// <param name="value">The string to assign to the value of the specified property.</param>
        protected void SetStringRegistryValue( string name, string value ) {
            this.m_RegistryKey.SetValue( name, value );
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Dispose of this object
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of all event handlers that we created in the constructor
        /// </summary>
        /// <param name="bDisposing">True if we are in process of disposing using Dispose</param>
        private void Dispose( bool bDisposing ) {
            // Check to see if Dispose has already been called.
            if( !this.m_bDisposed && bDisposing ) {
                // Tear down all listeners to changes in the attributes
                foreach( PropertyInfo property in this.m_PropertyPublisher.GetType().GetProperties() ) {
                    object[] publishAttribs = property.GetCustomAttributes( typeof(PropertyPublisher.PublishedAttribute), true );
                    object[] savedAttribs = property.GetCustomAttributes( typeof(PropertyPublisher.SavedAttribute), true );

                    // Unhook any handlers
                    if( savedAttribs.Length >= 1 && publishAttribs.Length >= 1 ) {
                        this.m_PropertyPublisher.Changed[property.Name].Remove(new PropertyEventHandler(this.OnPropertyChanged));
                    }
                }
            }
            this.m_bDisposed = true;
        }

        /// <summary>
        /// Destructs the object to ensure we do the cleanup, in case we don't call Dispose.
        /// </summary>
        ~RegistryService() {
            this.Dispose(false);
        }

        #endregion
    }
}
