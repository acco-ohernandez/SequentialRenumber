namespace SequentialRenumber
{
    /// <summary>
    /// Ribbon bootstrap for the dev sandbox: creates the temporary <c>Dev Tools</c> tab
    /// with the single <c>Sequential Renumber</c> button so the tool can be launched in
    /// Revit 2023 without touching the production ACCO ribbons.
    /// </summary>
    internal class App_SequentialRenumber : IExternalApplication
    {
        /// <summary>Builds the Dev Tools tab and its single button.</summary>
        public Result OnStartup(UIControlledApplication app)
        {
            string tabName = "ORH Dev";
            try
            {
                app.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                // Another dev add-in already created the tab; reuse it.
                Debug.Print("Tab already exists.");
            }

            RibbonPanel panel = Common.Utils.CreateRibbonPanel(app, tabName, "In Development");

            PushButtonData btnData = Cmd_SequentialRenumberTool.GetButtonData();
            panel.AddItem(btnData);

            return Result.Succeeded;
        }

        /// <summary>Nothing to tear down; the window cleans itself up on close.</summary>
        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
