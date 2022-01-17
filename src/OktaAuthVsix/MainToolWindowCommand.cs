using IdentityModel.OidcClient;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace OktaAuthVsix
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class MainToolWindowCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("5341f500-de89-4909-a916-f2ec5878e4fb");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage _package;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainToolWindowCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private MainToolWindowCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static MainToolWindowCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return _package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in MainToolWindowCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new MainToolWindowCommand(package, commandService);
        }

        /// <summary>
        /// Shows the tool window when the menu item is clicked.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            _package.JoinableTaskFactory.RunAsync(async delegate
            {
                ToolWindowPane window = await _package.ShowToolWindowAsync(typeof(MainToolWindow), 0, true, _package.DisposalToken);
                if ((null == window) || (null == window.Frame))
                {
                    throw new NotSupportedException("Cannot create tool window");
                }

                await OktaAuthAsync();
            });
        }

        private async Task OktaAuthAsync()
        {
            var options = new OidcClientOptions()
            {
                Authority = "https://{YOUR_OKTA_AUTHORITY}.okta.com/",
                ClientId = "{YOUR_OCTA_CLIENT_ID}",
                Scope = "openid profile email", // Example scopes
                Browser = new AuthBrowser(),
                Policy = new Policy
                {
                    RequireIdentityTokenSignature = false
                }
            };

            var oidcClient = new OidcClient(options);

            LoginResult loginResult;
            try
            {
                loginResult = await oidcClient.LoginAsync();
            }
            catch (Exception exception)
            {
                ShowMessageBox($"Unexpected Error: {exception.Message}");
                return;
            }

            if (loginResult.IsError)
            {
                ShowMessageBox(
                    loginResult.Error == "UserCancel"
                    ? "The sign-in window was closed before authorization was completed."
                    : loginResult.Error);
            }
            else
            {
                ShowMessageBox(loginResult.User.Identity.Name);
            }
        }

        private void ShowMessageBox(string message, string title = "Okta Extension")
        {
            VsShellUtilities.ShowMessageBox(
                _package,
                message,
                title,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
