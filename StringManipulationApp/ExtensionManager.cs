using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppExtensions;  // App Extensions!!
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace StringManipulationApp
{
    class ExtensionManager
    {
        private ObservableCollection<Extension> _extensions;
        private string _contract;
        private CoreDispatcher _dispatcher;
        private AppExtensionCatalog _catalog;

        public ExtensionManager(string contract)
        {
            // extensions list   
            _extensions = new ObservableCollection<Extension>();

            // catalog & contract
            _contract = contract;
            _catalog = AppExtensionCatalog.Open(_contract);

            // using a method that uses the UI Dispatcher before initializing will throw an exception
            _dispatcher = null;
        }

        public ObservableCollection<Extension> Extensions
        {
            get { return _extensions; }
        }

        public string Contract
        {
            get { return _contract; }
        }

        // this sets up UI dispatcher and does initial extension scan
        public void Initialize(CoreDispatcher dispatcher)
        {
            #region Error Checking & Dispatcher Setup
            // check that we haven't already been initialized
            if (_dispatcher != null)
            {
                throw new ExtensionManagerException("Extension Manager for " + this.Contract + " is already initialized.");
            }

            // thread that initializes the extension manager has the dispatcher
            _dispatcher = dispatcher;
            #endregion

            // set up extension management events
            _catalog.PackageInstalled += Catalog_PackageInstalled;
            _catalog.PackageUpdated += Catalog_PackageUpdated;
            _catalog.PackageUninstalling += Catalog_PackageUninstalling;
            _catalog.PackageUpdating += Catalog_PackageUpdating;
            _catalog.PackageStatusChanged += Catalog_PackageStatusChanged;

            // Scan all extensions
            FindAllExtensions();
        }

        public async void FindAllExtensions()
        {
            #region Error Checking
            // make sure we have initialized
            if (_dispatcher == null)
            {
                throw new ExtensionManagerException("Extension Manager for " + this.Contract + " is not initialized.");
            }
            #endregion

            // load all the extensions currently installed
            IReadOnlyList<AppExtension> extensions = await _catalog.FindAllAsync();
            foreach (AppExtension ext in extensions)
            {
                // load this extension
                await LoadExtension(ext);
            }
        }

        private async void Catalog_PackageInstalled(AppExtensionCatalog sender, AppExtensionPackageInstalledEventArgs args)
        {
            await _dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () => {
                foreach (AppExtension ext in args.Extensions)
                {
                    await LoadExtension(ext);
                }
            });
        }

        // package has been updated, so reload the extensions
        private async void Catalog_PackageUpdated(AppExtensionCatalog sender, AppExtensionPackageUpdatedEventArgs args)
        {
            await _dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () => {
                foreach (AppExtension ext in args.Extensions)
                {
                    await LoadExtension(ext);
                }
            });
        }

        // package is updating, so just unload the extensions
        private async void Catalog_PackageUpdating(AppExtensionCatalog sender, AppExtensionPackageUpdatingEventArgs args)
        {
            await UnloadExtensions(args.Package);
        }

        // package is removed, so unload all the extensions in the package and remove it
        private async void Catalog_PackageUninstalling(AppExtensionCatalog sender, AppExtensionPackageUninstallingEventArgs args)
        {
            await RemoveExtensions(args.Package);
        }

        // package status has changed, could be invalid, licensing issue, app was on USB and removed, etc
        private async void Catalog_PackageStatusChanged(AppExtensionCatalog sender, AppExtensionPackageStatusChangedEventArgs args)
        {
            // get package status
            if (!(args.Package.Status.VerifyIsOK()))
            {
                // if it's offline unload only
                if (args.Package.Status.PackageOffline)
                {
                    await UnloadExtensions(args.Package);
                }

                // package is being serviced or deployed
                else if (args.Package.Status.Servicing || args.Package.Status.DeploymentInProgress)
                {
                    // ignore these package status events
                }

                // package is tampered or invalid or some other issue
                // glyphing the extensions would be a good user experience
                else
                {
                    await RemoveExtensions(args.Package);
                }

            }
            // if package is now OK, attempt to load the extensions
            else
            {
                // try to load any extensions associated with this package
                await LoadExtensions(args.Package);
            }
        }

        // loads an extension
        public async Task LoadExtension(AppExtension ext)
        {
            // get unique identifier for this extension
            string identifier = ext.AppInfo.AppUserModelId + "!" + ext.Id;

            // load the extension if the package is OK
            if (!(ext.Package.Status.VerifyIsOK()
                    // This is where we'd normally do signature verfication, but for demo purposes it isn't important
                    // Below is an example of how you'd ensure that you only load store-signed extensions :)
                    //&& ext.Package.SignatureKind == PackageSignatureKind.Store
                    ))
            {
                // if this package doesn't meet our requirements
                // ignore it and return
                return;
            }

            // if its already existing then this is an update
            Extension existingExt = _extensions.Where(e => e.UniqueId == identifier).FirstOrDefault();

            // new extension
            if (existingExt == null)
            {
                // get extension properties
                var properties = await ext.GetExtensionPropertiesAsync() as PropertySet;

                // get logo 
                var filestream = await (ext.AppInfo.DisplayInfo.GetLogo(new Windows.Foundation.Size(1, 1))).OpenReadAsync();
                BitmapImage logo = new BitmapImage();
                logo.SetSource(filestream);

                // create new extension
                Extension nExt = new Extension(ext, properties, logo);

                // Add it to extension list
                _extensions.Add(nExt);

                // load it
                await nExt.Load();
            }
            // update
            else
            {
                // unload the extension
                existingExt.Unload();

                // update the extension
                await existingExt.Update(ext);
            }
        }

        // loads all extensions associated with a package - used for when package status comes back
        public async Task LoadExtensions(Package package)
        {
            await _dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                _extensions.Where(ext => ext.AppExtension.Package.Id.FamilyName == package.Id.FamilyName).ToList().ForEach(async e => { await e.Load(); });
            });
        }

        // unloads all extensions associated with a package - used for updating and when package status goes away
        public async Task UnloadExtensions(Package package)
        {
            await _dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                _extensions.Where(ext => ext.AppExtension.Package.Id.FamilyName == package.Id.FamilyName).ToList().ForEach(e => { e.Unload(); });
            });
        }

        // removes all extensions associated with a package - used when removing a package or it becomes invalid
        public async Task RemoveExtensions(Package package)
        {
            await _dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                _extensions.Where(ext => ext.AppExtension.Package.Id.FamilyName == package.Id.FamilyName).ToList().ForEach(e => { e.Unload(); _extensions.Remove(e); });
            });
        }


        public async void RemoveExtension(Extension ext)
        {
            await _catalog.RequestRemovePackageAsync(ext.AppExtension.Package.Id.FullName);
        }

        #region Extra exceptions
        // For exceptions using the Extension Manager
        public class ExtensionManagerException : Exception
        {
            public ExtensionManagerException() { }

            public ExtensionManagerException(string message) : base(message) { }

            public ExtensionManagerException(string message, Exception inner) : base(message, inner) { }
        }
        #endregion
    }

    public class Extension : INotifyPropertyChanged
    {
        #region Member Vars
        private PropertySet _properties;
        private string _serviceName;
        private readonly object _sync = new object();

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        public Extension(AppExtension ext, PropertySet properties, BitmapImage logo)
        {
            AppExtension = ext;
            _properties = properties;
            Enabled = false;
            Loaded = false;
            Offline = false;
            Logo = logo;
            Visible = Visibility.Collapsed;

            #region Properties
            _serviceName = null;
            if (_properties != null)
            {
                if (_properties.ContainsKey("Service"))
                {
                    PropertySet serviceProperty = _properties["Service"] as PropertySet;
                    _serviceName = serviceProperty["#text"].ToString();
                }
            }
            #endregion

            //AUMID + Extension ID is the unique identifier for an extension
            UniqueId = ext.AppInfo.AppUserModelId + "!" + ext.Id;
        }

        #region Properties
        public BitmapImage Logo { get; private set; }

        public string UniqueId { get; private set; } // the unique id of this extension which will be AppUserModel Id + Extension ID

        public bool Enabled { get; private set; } // whether the user has enabled the extension or not

        public bool Offline { get; private set; } // whether the package containing the extension is offline

        public bool Loaded { get; private set; } // whether the package has been loaded or not.

        public AppExtension AppExtension { get; private set; }

        public Visibility Visible { get; private set; } // Whether the extension should be visible in the list of extensions
        #endregion

        // this calls the 'extensionLoad' function in the script file, if it exists.
        public async Task<string> Invoke(ValueSet message)
        {
            if (Loaded)
            {
                try
                {
                    // make the app service call
                    using (var connection = new AppServiceConnection())
                    {
                        // service name is defined in appxmanifest properties
                        connection.AppServiceName = _serviceName;
                        // package Family Name is provided by the extension
                        connection.PackageFamilyName = AppExtension.Package.Id.FamilyName;

                        // open the app service connection
                        AppServiceConnectionStatus status = await connection.OpenAsync();
                        if (status != AppServiceConnectionStatus.Success)
                        {
                            Debug.WriteLine("Failed App Service Connection");
                        }
                        else
                        {
                            // Call the app service
                            AppServiceResponse response = await connection.SendMessageAsync(message);
                            if (response.Status == AppServiceResponseStatus.Success)
                            {
                                ValueSet answer = response.Message as ValueSet;
                                if (answer.ContainsKey("Result")) // When our app service returns "Result", it means it succeeded
                                {
                                    return answer["Result"] as string;
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    Debug.WriteLine("Calling the App Service failed");
                }
            }
            return string.Empty;
        }

        public async Task Update(AppExtension ext)
        {
            // ensure this is the same uid
            string identifier = ext.AppInfo.AppUserModelId + "!" + ext.Id;
            if (identifier != this.UniqueId)
            {
                return;
            }

            // get extension properties
            var properties = await ext.GetExtensionPropertiesAsync() as PropertySet;

            // get logo 
            var filestream = await (ext.AppInfo.DisplayInfo.GetLogo(new Windows.Foundation.Size(1, 1))).OpenReadAsync();
            BitmapImage logo = new BitmapImage();
            logo.SetSource(filestream);

            // update the extension
            this.AppExtension = ext;
            this._properties = properties;
            this.Logo = logo;

            #region Update Properties

            // update app service information
            this._serviceName = null;
            if (this._properties != null)
            {
                if (this._properties.ContainsKey("Service"))
                {
                    PropertySet serviceProperty = this._properties["Service"] as PropertySet;
                    this._serviceName = serviceProperty["#text"].ToString();
                }
            }
            #endregion

            // load it
            await Load();
        }

        // this controls loading of the extension
        public async Task Load()
        {
            // make sure package is OK to load
            if (!AppExtension.Package.Status.VerifyIsOK())
            {
                return;
            }

            Enabled = true;

            // Don't reload
            if (Loaded)
            {
                return;
            }

            // The public folder is shared between the extension and the host.
            // We don't use it in this sample but you can see https://github.com/Microsoft/Build2016-B808-AppExtensibilitySample for an example of it can be used.
            StorageFolder folder = await AppExtension.GetPublicFolderAsync();

            Loaded = true;
            Visible = Visibility.Visible;
            RaisePropertyChanged("Visible");
            Offline = false;
        }

        // This enables the extension
        public async Task Enable()
        {
            // indicate desired state is enabled
            Enabled = true;

            // load the extension
            await Load();
        }

        // this is different from Disable, example: during updates where we only want to unload -> reload
        // Enable is user intention. Load respects enable, but unload doesn't care
        public void Unload()
        {
            // unload it
            lock (_sync)
            {
                if (Loaded)
                {
                    // see if package is offline
                    if (!AppExtension.Package.Status.VerifyIsOK() && !AppExtension.Package.Status.PackageOffline)
                    {
                        Offline = true;
                    }

                    Loaded = false;
                    Visible = Visibility.Collapsed;
                    RaisePropertyChanged("Visible");
                }
            }
        }

        // user-facing action to disable the extension
        public void Disable()
        {
            // only disable if it is enabled
            if (Enabled)
            {
                // set desired state to disabled
                Enabled = false;
                // unload the app
                Unload();
            }
        }
        #region PropertyChanged

        // typical property changed handler
        private void RaisePropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
        #endregion

    }
}
