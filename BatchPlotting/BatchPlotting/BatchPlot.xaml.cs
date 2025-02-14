﻿using AttUpdatecmd;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Interop;
//using Autodesk.AutoCAD.Runtime;
using Microsoft.Win32;
using MultiSelectionTreeView;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using static BatchPlotting.BatchPlotingPublisher;

namespace BatchPlotting
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class BatchPlot : Window
    {
        string databasePath = string.Empty;
        string settingsFilePath = string.Empty;
        string connectionString = string.Empty;
        string dwfSaveLocation = string.Empty;
        string pdfSaveLocation = string.Empty;
        string sopsDwgLocation = string.Empty;
        string detailedLogLocation = string.Empty;
        bool _detailedLog = false;
        bool _isSortingOn = false;
        private BatchProcessBar objProcessBar = null;
        private BatchPlotingSubscriber<double> _pbarMaximumSubscriber;
        private BatchPlotingSubscriber<double> _pbarValueSubscriber;
        private BatchPlotingSubscriber<string> _pbarStatusSubscriber;
        private BatchPlotingSubscriber<string> _pbarHeaderSubscriber;
        private BatchPlot _batchPlot;
        public BatchPlot(IntPtr ptHandler)
        {
            InitializeComponent();
            InitializePublishers();
            ClsProperties.IsCancelled = false;
            new WindowInteropHelper(this).Owner = ptHandler;
            ClsProperties.IsFormClosed = false;
            settingsFilePath = Assembly.GetExecutingAssembly().Location;
            settingsFilePath = @"C:\SOPS\Publish\Settings.ini";// Path.Combine(settingsFilePath.Substring(0, settingsFilePath.LastIndexOf("\\")), @"Publish\Settings.ini");
            IniFile iniFile = new IniFile(settingsFilePath);
            databasePath = iniFile.IniReadValue("ConnectionString", "SOPS_DATABASE");
            BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreatedPDFChecked = iniFile.IniReadValue("ConnectionString", "CREATE_PDF") == "OFF" ? false : true;
            BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreateDWFChecked = iniFile.IniReadValue("ConnectionString", "CREATE_DWF") == "OFF" ? false : true;
            BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdatePublishDateChecked = iniFile.IniReadValue("ConnectionString", "UPDATE_PUBLISH_DATE") == "OFF" ? false : true;
            BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdateSearchInfoChecked = iniFile.IniReadValue("ConnectionString", "UPDATE_SEARCH_INFO") == "OFF" ? false : true;
            dwfSaveLocation = iniFile.IniReadValue("ConnectionString", "DWG_SAVE_LOCATION");
            pdfSaveLocation = iniFile.IniReadValue("ConnectionString", "PDF_SAVE_LOCATION");
            sopsDwgLocation = iniFile.IniReadValue("ConnectionString", "SOPS_DRAWING_LOCATION");
            Log.detailedLogPath = iniFile.IniReadValue("ConnectionString", "DETAILED_LOG_LOCATION");
            _detailedLog = iniFile.IniReadValue("ConnectionString", "DETAILED_LOG").Equals("ON", StringComparison.InvariantCultureIgnoreCase) ? true : false;
            _isSortingOn = iniFile.IniReadValue("ConnectionString", "SORTING").Equals("ON", StringComparison.InvariantCultureIgnoreCase) ? true : false;
            chkDetailedLog.IsChecked = _detailedLog;
            connectionString = "Provider = Microsoft.ACE.OLEDB.12.0; Data Source =" + databasePath + ";Persist Security Info = False";


            _pbarMaximumSubscriber = new BatchPlotingSubscriber<double>(BatchPlotingPublisher.ProgressBarMaximumPublisher);
            _pbarMaximumSubscriber.Publisher.DataPublisher += Publisher_MaximumDataPublisher;

            _pbarValueSubscriber = new BatchPlotingSubscriber<double>(BatchPlotingPublisher.ProgressValuePublisher);
            _pbarValueSubscriber.Publisher.DataPublisher += Publisher_ValueDataPublisher;

            _pbarStatusSubscriber = new BatchPlotingSubscriber<string>(BatchPlotingPublisher.ProgressStatusPublisher);
            _pbarStatusSubscriber.Publisher.DataPublisher += Publisher_StatusDataPublisher;

            _pbarHeaderSubscriber = new BatchPlotingSubscriber<string>(BatchPlotingPublisher.ProgressHeaderPublisher);
            _pbarHeaderSubscriber.Publisher.DataPublisher += Publisher_HeaderDataPublisher;

            btnAscending.Visibility = _isSortingOn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            btnDescending.Visibility = _isSortingOn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

            btnCancel.IsEnabled = false;
        }

        private void menuImport_Click_1(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "XML Files (*.xml)|*.xml";
            openFileDialog.Multiselect = false;
            bool? res = openFileDialog.ShowDialog();
            if (!(bool)res)
                return;
            if (System.IO.File.Exists(openFileDialog.FileName))
            {
                var batchPublish = Extension.Create(openFileDialog.FileName);
                if (batchPublish == null)
                    return;
                lstBxDrawings.Items.Clear();
                batchPublish.Drawing.ForEach(x =>
                {
                    if (!x.isLeft)
                    {
                        ListViewItem lstItem = new ListViewItem();
                        lstItem.Content = x.Name;
                        lstItem.Tag = x.Path;
                        var isExists = lstBxDrawings.Items.OfType<ListViewItem>().ToList().Where(y => y.Content.Equals(x.Name)).Select(y => y).FirstOrDefault() != null ? true : false;
                        if (!isExists && File.Exists(x.Path))
                            lstBxDrawings.Items.Add(lstItem);
                    }
                    else
                    {
                        ListBoxItem treeViewItem = new ListBoxItem();
                        treeViewItem.Tag = x.Path;
                        treeViewItem.Content = System.IO.Path.GetFileNameWithoutExtension(x.Name);
                        var isExists = treeViewDocs.Items.OfType<ListBoxItem>().ToList().Where(y => y.Content.Equals(treeViewItem.Content)).Select(y => y).FirstOrDefault() != null ? true : false;
                        if (!isExists && File.Exists(x.Path))
                            treeViewDocs.Items.Add(treeViewItem);
                    }
                });
                treeViewDocs.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("Content", System.ComponentModel.ListSortDirection.Ascending));
                EnableDisableControls(true);
            }
        }

        private void menuOpenDoc_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new OpenFileDialog();
                openDialog.Filter = "AutoCAD Files (*.dwg)|*.dwg";
                openDialog.Multiselect = true;
                openDialog.ShowDialog();
                if (openDialog.FileNames.Count() == 0)
                    return;
                openDialog.FileNames.OfType<string>().ToList().ForEach(x =>
                {
                    ListBoxItem treeViewItem = new ListBoxItem();
                    treeViewItem.Tag = x;
                    treeViewItem.Content = System.IO.Path.GetFileNameWithoutExtension(x);
                    var isExists = treeViewDocs.Items.OfType<ListBoxItem>().ToList().Where(y => y.Content.Equals(treeViewItem.Content)).Select(y => y).FirstOrDefault() != null ? true : false;
                    if (!isExists)
                        treeViewDocs.Items.Add(treeViewItem);
                });
                treeViewDocs.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("Content", System.ComponentModel.ListSortDirection.Ascending));
                EnableDisableControls(true);
            }
            catch (Exception ex)
            {
                Log.WriteExceptionLog(ex.Message, MethodBase.GetCurrentMethod().Name);
            }
            finally
            {
                EnableDisableControls(true);
            }
        }


        private void menuPrint_Click_1(object sender, RoutedEventArgs e)
        {
            Plot_Options plotOptions = new Plot_Options();
            plotOptions.ShowDialog();

        }

        private void menuSave_Click_1(object sender, RoutedEventArgs e)
        {
            if (lstBxDrawings.Items.Count == 0 && treeViewDocs.Items.Count == 0)
            {
                MessageBox.Show("Please add items to save.", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Title = "Save As";
            saveFileDialog.Filter = "XML Files (*.xml)|*.xml";
            bool? res = saveFileDialog.ShowDialog();
            if (!(bool)res)
                return;

            BatchPlottingPublish batchPlotting = new BatchPlottingPublish();
            batchPlotting.Drawing = new List<Drawing>();
            List<Drawing> lstDrawings = new List<Drawing>();
            lstBxDrawings.Items.OfType<ListViewItem>().ToList().ForEach(x =>
            {
                Drawing drawing = new Drawing();
                drawing.Name = x.Content.ToString();
                drawing.Path = x.Tag.ToString();
                drawing.isLeft = false;
                lstDrawings.Add(drawing);
            });

            treeViewDocs.Items.OfType<ListBoxItem>().ToList().ForEach(x =>
            {
                if (!lstDrawings.Any(y => y.Name.Equals(x.Content.ToString(), StringComparison.InvariantCultureIgnoreCase)))
                {
                    Drawing drawing = new Drawing();
                    drawing.Name = x.Content.ToString();
                    drawing.Path = x.Tag.ToString();
                    drawing.isLeft = true;
                    lstDrawings.Add(drawing);
                }
            });

            if (lstDrawings.Any())
                batchPlotting.Drawing = lstDrawings;

            var serialize = batchPlotting.ToXMLString();

            if (!string.IsNullOrWhiteSpace(serialize))
            {
                File.WriteAllText(saveFileDialog.FileName, serialize);
                MessageBox.Show("File has been saved successfully.", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void menuExit_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnAdd_Click_1(object sender, RoutedEventArgs e)
        {
            if (treeViewDocs.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select atleast one item to add.", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            treeViewDocs.SelectedItems.OfType<ListBoxItem>().ToList().ForEach(x =>
            {
                ListViewItem lstItem = new ListViewItem();
                lstItem.Content = x.Content;
                lstItem.Tag = x.Tag;
                bool isExists = lstBxDrawings.Items.OfType<ListViewItem>().ToList().Where(y => y.Content.Equals(lstItem.Content)).Select(y => y).FirstOrDefault() != null ? true : false;
                if (!isExists)
                    lstBxDrawings.Items.Add(lstItem);
                x.IsSelected = false;
            });

            ButtonEnableAccordingListItemsAdd();
        }

        private void btnAddAll_Click_1(object sender, RoutedEventArgs e)
        {
            lstBxDrawings.Items.Clear();

            //var root = (MultipleSelectionTreeViewItem)treeViewDocs.Items[0];
            treeViewDocs.Items.OfType<ListBoxItem>().ToList().ForEach(x =>
            {
                ListViewItem lstItem = new ListViewItem();
                lstItem.Content = x.Content;
                lstItem.Tag = x.Tag;
                lstBxDrawings.Items.Add(lstItem);
            });
            ButtonEnableAccordingListItemsAdd();
            treeViewDocs.SelectedItems.OfType<ListBoxItem>().ToList().ForEach(x =>
            {
                x.IsSelected = false;
            });
        }

        private void ButtonEnableAccordingListItemsAdd()
        {
            btnRemove.IsEnabled = true;
            btnRemoveAll.IsEnabled = true;
            btnPublish.IsEnabled = true;
            menuSave.IsEnabled = true;
        }

        private void btnRemove_Click_1(object sender, RoutedEventArgs e)
        {
            if (lstBxDrawings.SelectedIndex == -1)
            {
                MessageBox.Show("Select atleast one item to remove.", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            //for (int i = 0; i < lstBxDrawings.SelectedItems.Count; i++)
            //{
            //    lstBxDrawings.Items.RemoveAt(lstBxDrawings.Items.IndexOf(lstBxDrawings.Items[i]));
            //}

            lstBxDrawings.Items.OfType<ListBoxItem>().ToList().ForEach(x =>
            {
                if (x.IsSelected)
                {
                    lstBxDrawings.Items.Remove(x);
                }
            });

            if (lstBxDrawings.Items.Count == 0)
            {
                btnPublish.IsEnabled = false;
                menuSave.IsEnabled = false;
            }
            treeViewDocs.SelectedItems.OfType<ListBoxItem>().ToList().ForEach(x =>
            {
                x.IsSelected = false;
            });
        }

        private void btnRemoveAll_Click_1(object sender, RoutedEventArgs e)
        {
            if (lstBxDrawings.Items.Count == 0)
            {
                MessageBox.Show("No items exists to remove.", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            lstBxDrawings.Items.Clear();
            btnRemoveAll.IsEnabled = false;
            btnRemove.IsEnabled = false;
            btnPublish.IsEnabled = false;
            menuSave.IsEnabled = false;
            treeViewDocs.SelectedItems.OfType<ListBoxItem>().ToList().ForEach(x =>
            {
                x.IsSelected = false;
            });
        }

        private string GetUserId()
        {
            BatchPlottingProperties.BatchPlottingPropertiesInstance.UserId = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            return BatchPlottingProperties.BatchPlottingPropertiesInstance.UserId;
        }

        private ObjectIdCollection GetEntitiesOnLayer(string layerName)
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Autodesk.AutoCAD.EditorInput.Editor ed = doc.Editor;

            // Build a filter list so that only entities
            // on the specified layer are selected

            TypedValue[] tvs = new TypedValue[1] { new TypedValue((int)DxfCode.LayerName, layerName) };
            Autodesk.AutoCAD.EditorInput.SelectionFilter sf = new Autodesk.AutoCAD.EditorInput.SelectionFilter(tvs); Autodesk.AutoCAD.EditorInput.PromptSelectionResult psr = ed.SelectAll(sf);

            if (psr.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return new ObjectIdCollection(psr.Value.GetObjectIds());
            else
                return new ObjectIdCollection();
        }

        private IEnumerable<ObjectId> GetTextEntitiesOnLayer(Database db, string layerName)
        {
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId btrId in blockTable)
                {
                    var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                    //var textClass = RXObject.GetClass(typeof(DBText));
                    //if (btr.IsLayout)
                    {
                        foreach (ObjectId id in btr)
                        {
                            //if (id.ObjectClass == textClass)
                            {
                                var text = (Entity)tr.GetObject(id, OpenMode.ForRead);
                                if (text.Layer.Equals(layerName, System.StringComparison.CurrentCultureIgnoreCase))
                                {
                                    yield return id;
                                }
                            }
                        }
                    }
                }
            }
        }

        public void moveLayerToBack(string layerName)
        {
            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;

                Database db = doc.Database;

                Editor ed = doc.Editor;




                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                        foreach (ObjectId id in lt)
                        {
                            LayerTableRecord ltr = tr.GetObject(id, OpenMode.ForRead) as LayerTableRecord;
                            if (ltr.Name.Contains(layerName))
                            {
                                layerName = ltr.Name;
                                break;
                            }
                        }



                        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                        BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                        DrawOrderTable dot = tr.GetObject(btr.DrawOrderTableId, OpenMode.ForWrite) as DrawOrderTable;
                        TypedValue[] tvs = new TypedValue[1] { new TypedValue((int)DxfCode.LayerName, layerName) };
                        SelectionFilter sf = new SelectionFilter(tvs);
                        PromptSelectionResult psr = ed.SelectAll(sf);
                        if (psr.Status == PromptStatus.OK)
                        {
                            dot.MoveToBottom(new ObjectIdCollection(psr.Value.GetObjectIds()));
                        }
                        tr.Commit();
                    }

                    ed.Regen();
                }
            }

            catch (Exception ex)
            {

            }
        }

        public List<string> XReftoBeReProcessed = new List<string>();
        public List<string> gridRefLayerNames = new List<string>();
        public bool XrefGraph()
        {
            XReftoBeReProcessed = new List<string>();
            gridRefLayerNames = new List<string>();
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            List<String> fileNames = new List<string>();
            using (doc.LockDocument())
            {

                try
                {
                    XrefGraph xg = db.GetHostDwgXrefGraph(true);
                    GraphNode root = xg.RootNode;
                    if (root.NumOut <= 0)
                        return false;
                    for (int o = 0; o < root.NumOut; o++)
                    {
                        if (ClsProperties.IsCancelled)
                        {
                            continue;
                        }
                        XrefGraphNode child = root.Out(o) as XrefGraphNode;
                        if (child.XrefStatus == XrefStatus.Resolved)
                        {
                            fileNames.Add(child.Database.Filename);
                        }
                    }
                }
                catch { return false; }
            }

            bool result = false;
            if (fileNames.Count > 0)
            {
                result = true;
                doc.CloseAndDiscard();
            }

            foreach (string str in fileNames)
            {
                if (ClsProperties.IsCancelled)
                {
                    continue;
                }
                if (System.IO.File.Exists(str))
                {
                    Document docPublish = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.Open(str, false);
                    Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument = docPublish;

                    try
                    {
                        using (DocumentLock docLock = docPublish.LockDocument())
                        {
                            //Switch of and Thaw the layers that endswith "GRIDREF"
                            using (Transaction acTrans = docPublish.Database.TransactionManager.StartTransaction())
                            {
                                // Open the Layer table for read
                                LayerTable acLyrTbl;
                                acLyrTbl = acTrans.GetObject(docPublish.Database.LayerTableId, OpenMode.ForWrite) as LayerTable;

                                string sLayerNameEndsWith = "GRIDREF";
                                foreach (ObjectId acObjId in acLyrTbl)
                                {
                                    if (ClsProperties.IsCancelled)
                                    {
                                        acTrans.Commit();
                                        continue;
                                    }
                                    LayerTableRecord acLyrTblRec;
                                    acLyrTblRec = acTrans.GetObject(acObjId, OpenMode.ForWrite) as LayerTableRecord;

                                    if (acLyrTblRec.Name.ToString().EndsWith(sLayerNameEndsWith, StringComparison.InvariantCultureIgnoreCase) || acLyrTblRec.Name.ToString().StartsWith(sLayerNameEndsWith, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        if (acLyrTblRec.IsOff)
                                            acLyrTblRec.IsOff = false;
                                        if (acLyrTblRec.IsFrozen)
                                            acLyrTblRec.IsFrozen = false;

                                        if (!gridRefLayerNames.Contains(acLyrTblRec.Name))
                                            gridRefLayerNames.Add(acLyrTblRec.Name);

                                    }
                                }
                                acTrans.Commit();
                            }
                        }
                        using (docPublish.LockDocument())
                        {

                            foreach (string strLayerName in gridRefLayerNames)
                            {
                                if (ClsProperties.IsCancelled)
                                {
                                    continue;
                                }
                                //ObjectIdCollection oidCol = GetEntitiesOnLayer(strLayerName);
                                //if (oidCol != null)
                                //    if (oidCol.Count > 0)
                                //        MoveEnititesToBottom(docPublish.Database, oidCol);
                                moveLayerToBack(strLayerName);
                            }
                        }

                        #region Send Entities back if the layer name has "GRID REF"


                        foreach (string strLayerName in gridRefLayerNames)
                        {
                            if (ClsProperties.IsCancelled)
                            {
                                break;
                            }


                            Editor ed = docPublish.Editor;
                            using (docPublish.LockDocument())
                            {
                                using (Transaction tr = docPublish.Database.TransactionManager.StartTransaction())
                                {
                                    BlockTable bt = (BlockTable)tr.GetObject(docPublish.Database.BlockTableId, OpenMode.ForRead);
                                    foreach (ObjectId btrId in bt)
                                    {
                                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForWrite);
                                        DrawOrderTable dot = tr.GetObject(btr.DrawOrderTableId, OpenMode.ForWrite) as DrawOrderTable;
                                        ObjectIdCollection oidCol = new ObjectIdCollection();
                                        if (btr.IsFromExternalReference)
                                        {
                                            foreach (ObjectId id in btr)
                                            {
                                                Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                                                if (ent.Layer.Contains(strLayerName))
                                                {
                                                    oidCol.Add(id);
                                                }
                                            }

                                            dot.MoveToBottom(oidCol);
                                        }
                                    }
                                    tr.Commit();
                                }
                            }
                            ed.Regen();

                        }

                        #endregion

                        if (!XReftoBeReProcessed.Contains(str))
                            XReftoBeReProcessed.Add(str);
                        docPublish.CloseAndSave(docPublish.Name);

                    }
                    catch (Exception ex)
                    {

                    }
                }
            }

            return result;
        }


        public static void XrefLayer(string layerName)
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction Tx = db.TransactionManager.StartTransaction())
            {
                XrefGraph xg = db.GetHostDwgXrefGraph(true);
                GraphNode root = xg.RootNode;
                printChildren(root, ed, Tx, layerName);
                Tx.Commit();
            }
        }

        // Recursively prints out information about the XRef's hierarchy
        private static void printChildren(GraphNode i_root, Editor i_ed, Transaction i_Tx, string layerName)
        {
            for (int o = 0; o < i_root.NumOut; o++)
            {
                XrefGraphNode child = i_root.Out(o) as XrefGraphNode;
                if (child.XrefStatus == XrefStatus.Resolved)
                {
                    BlockTableRecord btr = i_Tx.GetObject(child.BlockTableRecordId, OpenMode.ForRead) as BlockTableRecord;

                    DrawOrderTable dot = i_Tx.GetObject(btr.DrawOrderTableId, OpenMode.ForWrite) as DrawOrderTable;
                    TypedValue[] tvs = new TypedValue[1] { new TypedValue((int)DxfCode.LayerName, layerName) };
                    SelectionFilter sf = new SelectionFilter(tvs);
                    PromptSelectionResult psr = i_ed.SelectAll(sf);
                    if (psr.Status == PromptStatus.OK)
                    {
                        dot.MoveToBottom(new ObjectIdCollection(psr.Value.GetObjectIds()));
                    }

                    //i_ed.WriteMessage("\n" + i_indent + child.Database.Filename);
                    // Name of the Xref (found name)
                    // You can find the original path too:
                    //if (bl.IsFromExternalReference == true)
                    // i_ed.WriteMessage("\n" + i_indent + "Xref path name: "+ bl.PathName);

                    printChildren(child, i_ed, i_Tx, layerName);

                }

            }

        }




        private void resetXREFFiles()
        {
            foreach (string str in XReftoBeReProcessed)
            {
                if (System.IO.File.Exists(str))
                {
                    Document docPublish = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.Open(str, false);
                    Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument = docPublish;

                    try
                    {
                        using (DocumentLock docLock = docPublish.LockDocument())
                        {
                            //Switch of and Thaw the layers that endswith "GRIDREF"
                            using (Transaction acTrans = docPublish.Database.TransactionManager.StartTransaction())
                            {
                                // Open the Layer table for read
                                LayerTable acLyrTbl;
                                acLyrTbl = acTrans.GetObject(docPublish.Database.LayerTableId, OpenMode.ForWrite) as LayerTable;

                                string sLayerNameEndsWith = "GRIDREF";
                                foreach (ObjectId acObjId in acLyrTbl)
                                {
                                    LayerTableRecord acLyrTblRec;
                                    acLyrTblRec = acTrans.GetObject(acObjId, OpenMode.ForWrite) as LayerTableRecord;

                                    if (acLyrTblRec.Name.ToString().EndsWith(sLayerNameEndsWith, StringComparison.InvariantCultureIgnoreCase) || acLyrTblRec.Name.ToString().StartsWith(sLayerNameEndsWith, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        if (!acLyrTblRec.IsOff)
                                            acLyrTblRec.IsOff = true;
                                        if (!acLyrTblRec.IsFrozen)
                                            acLyrTblRec.IsFrozen = true;
                                    }
                                }
                                acTrans.Commit();
                            }
                        }

                        docPublish.CloseAndSave(docPublish.Name);

                    }
                    catch { }
                }
            }
            XReftoBeReProcessed.Clear();
        }

        bool isStarted = false;
        private void btnPublish_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lstBxDrawings.Items.Count == 0)
                {
                    MessageBox.Show("No Drawing files selected.", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreateDWFChecked && !BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreatedPDFChecked && !BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdatePublishDateChecked && !BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdateSearchInfoChecked)
                {
                    MessageBox.Show("Please select one of the below options to publish." + Environment.NewLine + "    1.Create DWF" + Environment.NewLine + "    2.Create PDF" + Environment.NewLine + "    3.Update Publish Date" + Environment.NewLine + "    4.Update Search Info", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                MessageBoxResult result = default(MessageBoxResult);
                if (BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreateDWFChecked && BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreatedPDFChecked && BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdatePublishDateChecked && BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdateSearchInfoChecked &&
                    chkDetailedLog.IsChecked == true)
                {
                    result = MessageBoxResult.Yes;
                }
                else
                    result = MessageBox.Show("You have not selected the following options. Do you still wish to continue?" + Environment.NewLine + (BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreateDWFChecked ? string.Empty : "Create DWF" + Environment.NewLine) + (BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreatedPDFChecked ? string.Empty : "Create PDF" + Environment.NewLine) + (BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdatePublishDateChecked ? string.Empty : "Update Publish Date" + Environment.NewLine) + (BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdateSearchInfoChecked ? string.Empty : "Update Search Info" + Environment.NewLine) + ((chkDetailedLog.IsChecked == true) ? string.Empty : "Detailed Log"), "Batch Plotting", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    //objProcessBar = new BatchProcessBar(this,Process.GetCurrentProcess().MainWindowHandle);
                    //objProcessBar.ShowDialog();
                    ClsProperties.IsCancelled = false;
                    if (lstBxDrawings.Items.Count > 0)
                    {
                        btnCancel.IsEnabled = true;
                        btnPublish.IsEnabled = false;
                        gridProcess.Visibility = System.Windows.Visibility.Visible;
                        ProgressHeaderPublisher.PublishData("Processing...");
                        ProgressStatusPublisher.PublishData("");
                        ProgressBarMaximumPublisher.PublishData(0);
                        pbStatus.Visibility = System.Windows.Visibility.Visible;
                        txtPercentage.Visibility = System.Windows.Visibility.Visible;
                        txtPercentage.Text = string.Empty;
                        DateTime dateTime = DateTime.Now;
                        Log.logName = string.Format("{0}{1}{2}{3}{4}{5}", dateTime.Day, dateTime.Month, dateTime.Year, dateTime.Hour, dateTime.Minute, dateTime.Second);
                        DoProcess();
                        pbStatus.Visibility = System.Windows.Visibility.Collapsed;
                        txtPercentage.Visibility = System.Windows.Visibility.Collapsed;
                        EnableDisableControls(true);
                        resetXREFFiles();
                        //gridProcess.Visibility = System.Windows.Visibility.Collapsed;
                        btnCancel.IsEnabled = false;
                        btnPublish.IsEnabled = true;
                    }
                }
                //}
            }
            catch (Exception ex)
            {
                btnCancel.IsEnabled = false;
                btnPublish.IsEnabled = true;
                EnableDisableControls(true);
                ProgressHeaderPublisher.PublishData("Process cancelled by tool. Please do check log for reason.");
                Log.WriteExceptionLog(ex.Message, MethodBase.GetCurrentMethod().Name);
                pbStatus.Visibility = System.Windows.Visibility.Collapsed;
                txtPercentage.Visibility = System.Windows.Visibility.Collapsed;
                txtPercentage.Text = string.Empty;
            }
        }


        public void DoProcess()
        {
            if (BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreateDWFChecked)
            {
                if (!Directory.Exists(dwfSaveLocation))
                {
                    MessageBox.Show("DWF save location does not exist.", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
                    //Log.WriteExceptionLog("Database does not exist in " + databasePath, MethodBase.GetCurrentMethod().Name);
                    Log.WriteDebugLog("General", "General", GetUserId(), DateTime.Now, "DWF save location does not exist in " + databasePath, false, true);
                    ProgressHeaderPublisher.PublishData("DWF location does not exist...");
                    return;
                }
            }

            if (BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreatedPDFChecked)
            {
                if (!Directory.Exists(pdfSaveLocation))
                {
                    MessageBox.Show("PDF save location does not exist.", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
                    //Log.WriteExceptionLog("Database does not exist in " + databasePath, MethodBase.GetCurrentMethod().Name);
                    Log.WriteDebugLog("General", "General", GetUserId(), DateTime.Now, "PDF save location does not exist in " + databasePath, false, true);
                    ProgressHeaderPublisher.PublishData("PDF location does not exist...");
                    return;
                }
            }

            if (BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdateSearchInfoChecked)
            {
                if (!File.Exists(databasePath))
                {
                    MessageBox.Show("Database does not exist in " + databasePath, "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
                    Log.WriteExceptionLog("Database does not exist in " + databasePath, MethodBase.GetCurrentMethod().Name);
                    Log.WriteDebugLog("General", "General", GetUserId(), DateTime.Now, "Database does not exist in " + databasePath, false, true);
                    ProgressHeaderPublisher.PublishData("Database not found...");
                    return;
                }
            }
            EnableDisableControls(false);


            //EnsureAutoCadIsRunning(_autocadClassId);

            // if (_application != null)
            //{
            // _application.Visible = true;
            //Close all existing drawings
            //try
            //{
            //    _application.Documents.Close();
            //}
            //catch { }


            //AcadApplication _cadApplication = (AcadApplication)Autodesk.AutoCAD.ApplicationServices.Application.AcadApplication;
            // _cadApplication.ActiveDocument.Export
            Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.CloseAll();
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            //string dwgPath = doc.Name;

            //doc.CloseAndSave(dwgPath);

            //Log.WriteExceptionLog(databasePath, MethodBase.GetCurrentMethod().Name);
            //MessageBox.Show("Process started.", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
            int k = 0;
            int processCount = 0;
            ProgressBarMaximumPublisher.PublishData(lstBxDrawings.Items.Count);
            lstBxDrawings.Items.OfType<ListViewItem>().ToList().ForEach(x =>
            {
                try
                {
                    if (ClsProperties.IsFormClosed)
                    {
                        return;
                    }
                    if (ClsProperties.IsCancelled)
                    {
                        ProgressHeaderPublisher.PublishData("Process Cancelled");
                        return;
                    }
                    if (File.Exists(x.Tag.ToString()))
                    {
                        x.IsSelected = true;
                        processCount++;
                        ProgressHeaderPublisher.PublishData("Processing " + processCount + " of " + lstBxDrawings.Items.Count);

                        Document docPublish = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.Open(x.Tag.ToString(), false);
                        Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument = docPublish;


                        bool IsDocClosed = XrefGraph();

                        //Since Active document closed for updating external references
                        if (IsDocClosed)
                        {
                            docPublish = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.Open(x.Tag.ToString(), false);
                            Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument = docPublish;
                        }



                        #region Send Entities back if the layer name has "GRID REF"


                        foreach (string strLayerName in gridRefLayerNames)
                        {
                            if (ClsProperties.IsCancelled)
                            {
                                return;
                            }


                            Editor ed = docPublish.Editor;
                            using (docPublish.LockDocument())
                            {
                                using (Transaction tr = docPublish.Database.TransactionManager.StartTransaction())
                                {
                                    BlockTable bt = tr.GetObject(docPublish.Database.BlockTableId, OpenMode.ForWrite) as BlockTable;
                                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                                    DrawOrderTable dot = tr.GetObject(btr.DrawOrderTableId, OpenMode.ForWrite) as DrawOrderTable;
                                    TypedValue[] tvs = new TypedValue[1] { new TypedValue((int)DxfCode.Start, "INSERT") };
                                    SelectionFilter sf = new SelectionFilter(tvs);
                                    PromptSelectionResult psr = ed.SelectAll(sf);
                                    if (psr.Status == PromptStatus.OK)
                                    {
                                        ObjectIdCollection oidcol = new ObjectIdCollection();
                                        foreach (ObjectId oid in psr.Value.GetObjectIds())
                                        {
                                            BlockReference brNew = (BlockReference)tr.GetObject(oid, OpenMode.ForRead);
                                            BlockTableRecord btrNew = tr.GetObject(brNew.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                            if (btrNew.IsFromExternalReference)
                                                oidcol.Add(oid);
                                        }
                                        dot.MoveToBottom(oidcol);
                                    }
                                    tr.Commit();
                                    //BlockTable bt = (BlockTable)tr.GetObject(docPublish.Database.BlockTableId, OpenMode.ForRead);
                                    //foreach (ObjectId btrId in bt)
                                    //{
                                    //    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForWrite);
                                    //    DrawOrderTable dot = tr.GetObject(btr.DrawOrderTableId, OpenMode.ForWrite) as DrawOrderTable;
                                    //    ObjectIdCollection oidCol = new ObjectIdCollection();
                                    //    if (btr.IsFromExternalReference)
                                    //    {
                                    //        foreach (ObjectId id in btr)
                                    //        {
                                    //            Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                                    //            //  if (ent.Layer.Contains(strLayerName))
                                    //            {
                                    //                oidCol.Add(id);
                                    //            }
                                    //        }

                                    //        dot.MoveToBottom(oidCol);
                                    //    }
                                    //}
                                    //tr.Commit();
                                }
                            }
                            ed.Regen();


                            //using (docPublish.LockDocument())
                            //{
                            //    string layerName = strLayerName;
                            //    using (Transaction tr = docPublish.TransactionManager.StartTransaction())
                            //    {
                            //        LayerTable lt = tr.GetObject(docPublish.Database.LayerTableId, OpenMode.ForRead) as LayerTable;
                            //        foreach (ObjectId id in lt)
                            //        {
                            //            LayerTableRecord ltr = tr.GetObject(id, OpenMode.ForRead) as LayerTableRecord;
                            //            if (ltr.Name.Contains(layerName))
                            //            {
                            //                layerName = ltr.Name;
                            //                break;
                            //            }
                            //        }
                            //        tr.Commit();
                            //    }
                            //    // moveLayerToBack(layerName);
                            //    XrefLayer(layerName);
                            //}
                        }
                        docPublish.CloseAndSave(docPublish.Name);

                        docPublish = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.Open(x.Tag.ToString(), false);
                        Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument = docPublish;
                        // trans.Commit();
                        // }
                        #endregion

                        using (docPublish.LockDocument())
                        {
                            string docName = docPublish.Name;

                            var dateTime = DateTime.Now;
                            //Log.WriteDebugLog(docName, docName, GetUserId(), dateTime, "", true, false);

                            ////doc.SendCommand("(command \"SECURELOAD\" 0) ");//Disable dll load warning
                            ////doc.SendCommand("(command \"FILEDIA\" 0) ");
                            ////docPublish.SendStringToExecute("(command \"BACKGROUNDPLOT\" 0) ", true, false, false);
                            //docPublish.SendStringToExecute("BACKGROUNDPLOT", true, false, false);
                            ////doc.SendCommand("(command " + (char)34 + "Netload" + (char)34 + " " + (char)34 + "C:/MGDS/AttUpdatecmd.dll" + (char)34 + ") ");




                            string outputFileName = "";
                            bool isProcessed = false;
                            if (BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreateDWFChecked)
                            {
                                if (ClsProperties.IsCancelled)
                                {
                                    ProgressHeaderPublisher.PublishData("Process Cancelled");
                                    return;
                                }
                                if (Directory.Exists(dwfSaveLocation))
                                {
                                    ProgressStatusPublisher.PublishData("Writing DWF...");
                                    Log.WriteDebugLog(docName, docName, GetUserId(), DateTime.Now, "Creating DWF for " + docName + ". Started:" + DateTime.Now.ToString(), true, false);
                                    outputFileName = x.Tag.ToString().Replace("dwg", "dwf");
                                    if (File.Exists(outputFileName))
                                        File.Delete(outputFileName);
                                    // doc.SendCommand("PublishDWF ");

                                    Commands.PublishDWF(dwfSaveLocation, out isProcessed, _detailedLog);//.PublishDrawing("dwf");
                                                                                                        //checkFile(outputFileName);
                                    Log.WriteDebugLog(docName, docName, GetUserId(), DateTime.Now, "Completed DWF for " + docName + ". Completed:" + DateTime.Now.ToString(), false, false);
                                    System.Threading.Thread.Sleep(3000);

                                    if (ClsProperties.IsCancelled)
                                    {
                                        ProgressHeaderPublisher.PublishData("Process Cancelled");
                                        //ProgressStatusPublisher.PublishData("Cacelled");
                                        return;
                                    }
                                }
                                else
                                {
                                    Log.WriteDebugLog(docName, docName, GetUserId(), DateTime.Now, "DWF location does not exist.", false, false);
                                }
                            }

                            if (BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreatedPDFChecked)
                            {
                                if (ClsProperties.IsCancelled)
                                {
                                    ProgressHeaderPublisher.PublishData("Process Cancelled");
                                    return;
                                }
                                if (Directory.Exists(pdfSaveLocation))
                                {
                                    ProgressStatusPublisher.PublishData("Writing PDF...");
                                    Log.WriteDebugLog(docName, docName, GetUserId(), DateTime.Now, "Creating PDF for " + docName + ". Started:" + DateTime.Now.ToString(), false, false);
                                    outputFileName = x.Tag.ToString().Replace("dwg", "pdf");
                                    if (File.Exists(outputFileName))
                                        File.Delete(outputFileName);
                                    Commands.PublishDrawing("pdf", pdfSaveLocation, out isProcessed, _detailedLog);
                                    //checkFile(outputFileName);
                                    Log.WriteDebugLog(docName, docName, GetUserId(), DateTime.Now, "Completed PDF for " + docName + ". Completed:" + DateTime.Now.ToString(), false, false);
                                    System.Threading.Thread.Sleep(3000);
                                    if (ClsProperties.IsCancelled)
                                    {
                                        ProgressHeaderPublisher.PublishData("Process Cancelled");
                                        //ProgressStatusPublisher.PublishData("Cacelled");
                                        return;
                                    }
                                }
                                else
                                {
                                    Log.WriteDebugLog(docName, docName, GetUserId(), DateTime.Now, "PDF location does not exist.", false, false);
                                }
                            }

                            //doc.SendCommand("(command \"SECURELOAD\" 1) ");//Disable dll load warning
                            // doc.SendCommand("(command \"FILEDIA\" 1) ");

                            if (BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdatePublishDateChecked)
                            {
                                if (ClsProperties.IsCancelled)
                                {
                                    ProgressHeaderPublisher.PublishData("Process Cancelled");
                                    return;
                                }
                                ProgressStatusPublisher.PublishData("Updating Publish Date...");
                                Log.WriteDebugLog(docName, docName, GetUserId(), DateTime.Now, "Updating Publish Date " + DateTime.Now.ToString(), false, false);
                                Database db = docPublish.Database;
                                UpdatePublishDate(db);
                                isProcessed = true;
                                Log.WriteDebugLog(docName, docName, GetUserId(), DateTime.Now, "Updated Publish Date " + DateTime.Now.ToString(), false, false);
                                if (ClsProperties.IsCancelled)
                                {
                                    ProgressHeaderPublisher.PublishData("Process Cancelled");
                                    //ProgressStatusPublisher.PublishData("Cacelled");
                                    return;
                                }
                            }
                            if (BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdateSearchInfoChecked)
                            {
                                //if (!File.Exists(databasePath))
                                //{
                                //    MessageBox.Show("Database does not exist in " + databasePath, "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
                                //    Log.WriteExceptionLog("Database does not exist in " + databasePath, MethodBase.GetCurrentMethod().Name);
                                //    Log.WriteDebugLog(docName, docName, GetUserId(), DateTime.Now, "Database does not exist in " + databasePath, false, true);
                                //}
                                //else
                                {
                                    if (ClsProperties.IsCancelled)
                                    {
                                        ProgressHeaderPublisher.PublishData("Process Cancelled");
                                        return;
                                    }
                                    ProgressStatusPublisher.PublishData("Updating SearchInfo...");
                                    if (!_detailedLog)
                                        Log.WriteDebugLog(docName, docName, GetUserId(), DateTime.Now, "Updating Search Info", false, false);
                                    Database db = docPublish.Database;
                                    UpdateSearchInfo(db, docPublish);
                                    isProcessed = true;
                                    if (!_detailedLog)
                                        Log.WriteDebugLog(docName, docName, GetUserId(), DateTime.Now, "Search Info update completed time", false, true);

                                    if (ClsProperties.IsCancelled)
                                    {
                                        ProgressHeaderPublisher.PublishData("Process Cancelled");
                                        //ProgressStatusPublisher.PublishData("Cacelled");
                                        return;
                                    }
                                }
                            }
                            ProgressValuePublisher.PublishData(1);
                            if (isProcessed)
                                k++;

                            Log.WriteDebugLog(docName, docName, GetUserId(), dateTime, "", false, true);
                        }
                        docPublish.CloseAndSave(docPublish.Name);
                        //docPublish.CloseAndDiscard();
                        //resetXREFFiles();
                        x.IsSelected = false;
                    }
                }
                catch (Exception ex)
                {
                    ProgressHeaderPublisher.PublishData(x.Tag + " Process cancelled by tool. Please do check log for reason.");
                    Log.WriteExceptionLog(ex.Message, MethodBase.GetCurrentMethod().Name);
                }
            });
            //Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument = doc;
            //_application.Quit();
            //_application = null;
            //MessageBox.Show("Number of files processed : " + k.ToString() + ".", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
            if (!ClsProperties.IsCancelled)
            {
                ProgressHeaderPublisher.PublishData("Processed " + k + " of " + lstBxDrawings.Items.Count + " Successfully");
                ProgressStatusPublisher.PublishData("Completed");
            }
            else
            {
                EnableDisableControls(true);
            }

        }


        private void DeleteRecordFromMDB(string dwgNumber)
        {
            try
            {
                OleDbConnection oleDbConnection = new OleDbConnection(connectionString);
                oleDbConnection.Open();
                string query = "delete * from Asset_EXTent where drg_number = '" + dwgNumber + "'";
                OleDbCommand oleDbCommand = new OleDbCommand(query, oleDbConnection);
                oleDbCommand.ExecuteNonQuery();
                oleDbConnection.Close();
            }
            catch (Exception ex)
            {
                Log.WriteExceptionLog("Error from deleting record from database.", MethodBase.GetCurrentMethod().Name);
            }
        }

        private static ObjectIdCollection MoveEnititesToBottom(Database db, ObjectIdCollection ids)
        {
            // The IDs of any block references we find
            // to return to the call for updating
            var brIds = new ObjectIdCollection();
            try
            {
                using (Transaction tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    // We only need to get this once

                    var wc = Autodesk.AutoCAD.Runtime.RXClass.GetClass(typeof(Entity)); //Wipeout));

                    // Take a copy of the IDs passed in, as we'll modify the
                    // original list for the caller to use

                    var btrIds = new ObjectId[ids.Count];
                    ids.CopyTo(btrIds, 0);

                    // Loop through the blocks passed in, opening each one

                    foreach (var btrId in btrIds)
                    {
                        var btr = (BlockReference)tr.GetObject(btrId, OpenMode.ForRead);

                        // Collect the wipeouts in the block


                        // var wipeouts = ids;
                        var wipeouts = new ObjectIdCollection();
                        //foreach (ObjectId id in btr)
                        {
                            var ent = (Entity)tr.GetObject(btr.ObjectId, OpenMode.ForRead);
                            if (ent.GetRXClass().IsDerivedFrom(wc))
                            {
                                wipeouts.Add(btr.ObjectId);
                            }
                        }

                        // Move the collected wipeouts to the bottom

                        if (wipeouts.Count > 0)
                        {
                            // Modify the draw order table, if we have wipepouts
                            var blkTableId = btr.BlockTableRecord;
                            var blkTable = (BlockTableRecord)tr.GetObject(blkTableId, OpenMode.ForRead);
                            var dot = (DrawOrderTable)tr.GetObject(blkTable.DrawOrderTableId, OpenMode.ForRead);
                            dot.MoveToBottom(wipeouts);

                            // Collect the block references to this block, to pass
                            // back to the calling function for updating

                            //var btrBrIds = btr.GetBlockReferenceIds(false, false);
                            //foreach (ObjectId btrBrId in btrBrIds)
                            //{
                            //    brIds.Add(btrBrId);
                            //}
                        }
                        else
                        {
                            ids.Remove(btrId);
                        }
                    }
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {

            }
            return brIds;
        }

        private int getNumberOfSheets()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            List<BlockReference> lstBlockReference = new List<BlockReference>();
            using (Transaction trans = doc.Database.TransactionManager.StartTransaction())
            {
                BlockTable btable = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                foreach (ObjectId objId in btable)
                {
                    if (ClsProperties.IsCancelled)
                    {
                        trans.Commit();
                        return 0;
                    }
                    BlockTableRecord blkTableRec = trans.GetObject(objId, OpenMode.ForWrite, false, true) as BlockTableRecord;
                    if (!blkTableRec.Name.ToUpper().StartsWith("PCC_FRAME") && (!blkTableRec.Name.ToUpper().StartsWith("FRAME")))
                        continue;
                    if (blkTableRec.Name.ToUpper().Contains("PCC_GRIDREF"))
                        continue;
                    if (blkTableRec.IsFromExternalReference && !(blkTableRec.Name.Contains("|")))
                        continue;

                    var blockRefIDs = blkTableRec.GetBlockReferenceIds(true, true);
                    foreach (ObjectId blockRefId in blockRefIDs)
                    {
                        Entity ent = trans.GetObject(blockRefId, OpenMode.ForWrite, false, true) as Entity;
                        if (ent != null)
                        {
                            BlockReference blkReference = ent as BlockReference;
                            if (!blkReference.Name.ToUpper().StartsWith("PCC_FRAME") && (!blkReference.Name.ToUpper().StartsWith("FRAME")))
                                continue;
                            lstBlockReference.Add(blkReference);
                        }
                    }
                }

                int i = 1;
                int s = 1;
                if (lstBlockReference.Count == 0)
                {
                    if (ClsProperties.IsCancelled)
                    {
                        trans.Commit();
                        return 0;
                    }
                    XrefGraph xg = db.GetHostDwgXrefGraph(true);
                    GraphNode root = xg.RootNode;
                    for (int o = 0; o < root.NumOut; o++)
                    {
                        XrefGraphNode child = root.Out(o) as XrefGraphNode;
                        if (child.XrefStatus == XrefStatus.Resolved)
                        {
                            BlockTableRecord bl = trans.GetObject(child.BlockTableRecordId, OpenMode.ForRead) as BlockTableRecord;
                            foreach (ObjectId blockRefId in bl)
                            {
                                Entity ent = trans.GetObject(blockRefId, OpenMode.ForWrite, false, true) as Entity;
                                if (ent != null)
                                {
                                    BlockReference blkReference = ent as BlockReference;
                                    var frameName = blkReference.Name.Contains("|") ? blkReference.Name.Substring(blkReference.Name.IndexOf("|") + 1) : string.Empty;
                                    if (!string.IsNullOrWhiteSpace(frameName) && frameName.Contains("PCC_FRAME"))
                                    {
                                        lstBlockReference.Add(blkReference);
                                    }
                                }
                            }
                        }
                    }
                    trans.Commit();
                }
                return lstBlockReference.Count();
            }
        }


        private void UpdateSearchInfo(Database db, Document doc)
        {
            try
            {
                string assetType = string.Empty;
                string assetNumSwitch = string.Empty;
                string assetNumbSub = string.Empty;
                string assetAddress = string.Empty;
                string diagramNum = string.Empty;
                string diagramName = string.Empty;
                double width = (doc.GetAcadDocument() as AcadDocument).Width;
                //MessageBox.Show(width.ToString());                

                //Hardcoded
                //1 sheet = 686 x 980
                //2 sheets = 1374 x 980
                //3 sheets = 2114 x 980
                //4 sheets = 2802 x 980
                //5 Sheets = 3516 x 980

                int NumberOfSheets = getNumberOfSheets();
                if (NumberOfSheets == 1)
                    width = 686;
                else if (NumberOfSheets == 2)
                    width = 1374;
                else if (NumberOfSheets == 3)
                    width = 2114;
                else if (NumberOfSheets == 4)
                    width = 2802;
                else if (NumberOfSheets == 5)
                    width = 3516;

                //Height is always fixed as 980 - 21-Dec-2017
                double height = 980; //(doc.GetAcadDocument() as AcadDocument).Height;
                //MessageBox.Show(width.ToString());
                diagramNum = Path.GetFileNameWithoutExtension(doc.Name);
                //MessageBox.Show(diagramNum);
                diagramNum = diagramNum.Substring(diagramNum.Length - 9);
                //  MessageBox.Show(diagramNum);
                diagramNum = diagramNum.Substring(diagramNum.Length - 5);
                // MessageBox.Show(diagramNum);
                //MessageBox.Show("Deleting in progress");
                DeleteRecordFromMDB(diagramNum);
                //MessageBox.Show("Deleting done");
                OleDbConnection oleDbConnection = new OleDbConnection(connectionString);
                try
                {
                    oleDbConnection.Open();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Problem in connecting with database. - " + ex.ToString());
                }
                DataSet loaddataset = new DataSet();
                OleDbCommandBuilder cb = new OleDbCommandBuilder();
                string loadsql = "Select * from Asset_EXTent";
                OleDbDataAdapter loadadapter = new OleDbDataAdapter(loadsql, oleDbConnection);
                loadadapter.Fill(loaddataset);
                cb = new OleDbCommandBuilder(loadadapter);
                // MessageBox.Show("1");
                loadadapter.TableMappings.Add("Asset_Extent", "Asset_Extent");
                var columMappings = loadadapter.TableMappings[0].ColumnMappings;
                columMappings.Add("ID", "ID");
                columMappings.Add("ASSET_TYPE", "ASSET_TYPE");
                columMappings.Add("ASSET_NUMBER", "ASSET_NUMBER");
                columMappings.Add("ASSET_ADDRESS", "ASSET_ADDRESS");
                columMappings.Add("DIAGRAM_NAME", "DIAGRAM_NAME");
                columMappings.Add("DRG_NUMBER", "DRG_NUMBER");
                columMappings.Add("X", "X");
                columMappings.Add("Y", "Y");
                columMappings.Add("DRG_WIDTH", "DRG_WIDTH");
                columMappings.Add("DRG_HEIGHT", "DRG_HEIGHT");
                oleDbConnection.Close();
                //  MessageBox.Show("2");
                diagramName = Path.GetFileNameWithoutExtension(doc.Name).Substring(0, Path.GetFileNameWithoutExtension(doc.Name).IndexOf("_"));
                var tableAsset1 = loaddataset.Tables[0];
                int maxId = GetMaxId();
                //tableAsset1.Rows.Add(maxId, diagramNum, "?", diagramName, diagramNum, 0, 0, 0, 0, 0);
                tableAsset1.Rows.Add(maxId, 0, "?", "", diagramName, diagramNum, 0, 0, 0, 0);
                loadadapter.InsertCommand = cb.GetInsertCommand();
                //Chocka - 29-12-2017
                loadadapter.Update(tableAsset1);// loaddataset.Tables[0]);


                //MessageBox.Show(diagramName.ToString());
                string x = string.Empty; string y = string.Empty;
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    BlockTable blockTable = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    foreach (ObjectId objId in blockTable)
                    {
                        if (ClsProperties.IsCancelled)
                        {
                            trans.Commit();
                            return;
                        }
                        BlockTableRecord blkTableRec = trans.GetObject(objId, OpenMode.ForRead) as BlockTableRecord;
                        if (blkTableRec.Name.ToUpper().StartsWith("SOPS_TRF_") || blkTableRec.Name.ToUpper().StartsWith("SOPS_SW_"))
                        {

                            var blockRefIDs = default(ObjectIdCollection);
                            if (blkTableRec.IsDynamicBlock)
                            {
                                var blockRefIDsNew = blkTableRec.GetAnonymousBlockIds();
                                var dynamicBlocksId = new ObjectIdCollection();
                                foreach (ObjectId item in blockRefIDsNew)
                                {
                                    BlockTableRecord anonymousBtr = (BlockTableRecord)trans.GetObject(item, OpenMode.ForRead);

                                    blockRefIDsNew = anonymousBtr.GetBlockReferenceIds(true, true);

                                    foreach (ObjectId id in blockRefIDsNew)
                                    {
                                        dynamicBlocksId.Add(id);
                                    }
                                }
                                blockRefIDs = dynamicBlocksId;
                            }
                            else
                                blockRefIDs = blkTableRec.GetBlockReferenceIds(true, true);
                            assetType = blkTableRec.Name.ToUpper().StartsWith("SOPS_TRF_") ? "1" : blkTableRec.Name.ToUpper().StartsWith("SOPS_SW_") ? "2" : string.Empty;
                            foreach (ObjectId blockRefId in blockRefIDs)
                            {
                                if (ClsProperties.IsCancelled)
                                {
                                    trans.Commit();
                                    return;
                                }
                                Entity ent = trans.GetObject(blockRefId, OpenMode.ForRead) as Entity;
                                if (ent != null)
                                {
                                    BlockReference blkReference = ent as BlockReference;
                                    if (blkReference.Name.Equals(blkTableRec.Name) || blkTableRec.IsDynamicBlock)
                                    {
                                        //if ((bool)chkDetailedLog.IsChecked)
                                        //{
                                        if (_detailedLog)
                                            Log.WriteDebugLog(string.Empty, string.Empty, GetUserId(), DateTime.Now, "Updating Search Info started for : " + blkTableRec.Name + ". Diagram Number : " + diagramNum + ". Asset Type : " + assetType + ". Time Started :" + DateTime.Now.ToString(), false, false, _detailedLog);
                                        //}
                                        x = blkReference.Position.X.ToString();
                                        y = blkReference.Position.Y.ToString();
                                        var attIdCollection = blkReference.AttributeCollection;
                                        bool isLocationFound = false, isSwitchFound = false, isSubNoFound = false;
                                        assetAddress = "";
                                        assetNumSwitch = "";
                                        assetNumbSub = "";
                                        foreach (ObjectId attr in attIdCollection)
                                        {


                                            if (ClsProperties.IsCancelled)
                                            {
                                                trans.Commit();
                                                return;
                                            }
                                            AttributeReference attRef = trans.GetObject(attr, OpenMode.ForRead) as AttributeReference;
                                            if (attRef.Tag.ToUpper().Equals("LOCATION"))
                                            {
                                                assetAddress = attRef.TextString;
                                                isLocationFound = !string.IsNullOrWhiteSpace(assetAddress) ? true : false;
                                                if (_detailedLog)
                                                    Log.WriteDebugLog(string.Empty, string.Empty, GetUserId(), DateTime.Now, "Asset Address :" + assetAddress, false, false, _detailedLog);
                                            }
                                            if (attRef.Tag.ToUpper().Equals("SWITCH_NUMBER"))
                                            {
                                                assetType = "2";
                                                assetNumSwitch = attRef.TextString;
                                                isSwitchFound = !string.IsNullOrWhiteSpace(assetNumSwitch) ? true : false;
                                                if (_detailedLog)
                                                    Log.WriteDebugLog(string.Empty, string.Empty, GetUserId(), DateTime.Now, "Asset Switch Number :" + assetNumSwitch, false, false, _detailedLog);
                                            }
                                            if (attRef.Tag.ToUpper().Equals("SUB_NUMBER"))
                                            {
                                                assetType = "1";
                                                assetNumbSub = attRef.TextString;
                                                isSubNoFound = !string.IsNullOrWhiteSpace(assetNumbSub) ? true : false;
                                                if (_detailedLog)
                                                    Log.WriteDebugLog(string.Empty, string.Empty, GetUserId(), DateTime.Now, "Asset Sub Number :" + assetNumbSub, false, false, _detailedLog);
                                            }
                                            if (attRef.Tag.ToUpper().Equals("POSITION X"))
                                            {
                                                x = attRef.TextString;
                                            }
                                            if (attRef.Tag.ToUpper().Equals("POSITION Y"))
                                            {
                                                y = attRef.TextString;
                                            }
                                        }
                                        if (!isLocationFound)
                                            Log.WriteDebugLog(string.Empty, string.Empty, GetUserId(), DateTime.Now, "Location attribute not found for " + blkReference.Name + " at " + x + "/" + y, false, false, _detailedLog);
                                        if (!isSwitchFound)
                                            Log.WriteDebugLog(string.Empty, string.Empty, GetUserId(), DateTime.Now, "SWITCH_NUMBER attribute not found for " + blkReference.Name + " at " + x + "/" + y, false, false, _detailedLog);
                                        if (!isSubNoFound)
                                            Log.WriteDebugLog(string.Empty, string.Empty, GetUserId(), DateTime.Now, "SUB_NUMBER attribute not found for " + blkReference.Name + " at " + x + "/" + y, false, false, _detailedLog);

                                        maxId = GetMaxId();
                                        bool isAdded = false;
                                        //   MessageBox.Show("3");
                                        var tableAsset = loaddataset.Tables[0];
                                        //   MessageBox.Show("4");
                                        if (!string.IsNullOrWhiteSpace(assetNumbSub))
                                        {
                                            tableAsset.Rows.Add(maxId, "1", assetNumbSub, assetAddress, diagramName, diagramNum, x, y, width * 1, height);
                                            isAdded = true;
                                        }
                                        if (!string.IsNullOrWhiteSpace(assetNumSwitch))
                                        {
                                            if (isAdded)
                                                maxId = maxId + 1;
                                            tableAsset.Rows.Add(maxId, "2", assetNumSwitch, assetAddress, diagramName, diagramNum, x, y, width * 1, height);
                                        }

                                        loadadapter.InsertCommand = cb.GetInsertCommand();
                                        //Chocka - 29-12-2017
                                        loadadapter.Update(tableAsset);// loaddataset.Tables[0]);
                                        if (_detailedLog)
                                            Log.WriteDebugLog(string.Empty, string.Empty, GetUserId(), DateTime.Now, "Updated Search Info completed for : " + blkTableRec.Name + ". Completed Time : " + DateTime.Now, false, false, _detailedLog);

                                    }
                                }
                            }
                        }
                    }
                    //var tableAsset1 = loaddataset.Tables[0];
                    //tableAsset1.Rows.Add(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                    //loadadapter.InsertCommand = cb.GetInsertCommand();
                    //loadadapter.Update(loaddataset.Tables[0]);

                }
            }
            catch (System.Exception exx)
            {
                var st = new StackTrace(exx, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();

                Log.WriteExceptionLog(connectionString, MethodBase.GetCurrentMethod().Name);
                Log.WriteExceptionLog(line.ToString(), MethodBase.GetCurrentMethod().Name);
                Log.WriteExceptionLog(exx.ToString(), MethodBase.GetCurrentMethod().Name);
            }
        }

        private int GetMaxId()
        {
            try
            {
                OleDbConnection oleDbConnection = new OleDbConnection(connectionString);
                oleDbConnection.Open();
                string query = "select MAX(ID) as Id from Asset_EXTent";
                OleDbCommand oleDbCommand = new OleDbCommand(query, oleDbConnection);
                System.Data.DataTable dt = new System.Data.DataTable();
                dt.Load(oleDbCommand.ExecuteReader());
                oleDbConnection.Close();
                if (dt.Rows.Count > 0)
                    return Convert.ToInt32(dt.Rows[0][0].ToString());
            }
            catch
            {

            }
            return 1;

        }

        private void UpdatePublishDate(Database db)
        {
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = trans.GetObject(db.BlockTableId, OpenMode.ForRead, false, true) as BlockTable;
                foreach (ObjectId objId in blockTable)
                {
                    BlockTableRecord blkTableRec = trans.GetObject(objId, OpenMode.ForWrite, false, true) as BlockTableRecord;
                    if (!blkTableRec.Name.StartsWith("DRAWING_INFO_SCHEM"))
                        continue;
                    var blockRefIDs = blkTableRec.GetBlockReferenceIds(true, true);
                    foreach (ObjectId blockRefId in blockRefIDs)
                    {
                        if (ClsProperties.IsCancelled)
                        {
                            trans.Commit();
                            return;
                        }
                        Entity ent = trans.GetObject(blockRefId, OpenMode.ForWrite, false, true) as Entity;
                        if (ent != null)
                        {
                            BlockReference blkReference = ent as BlockReference;
                            var attIdCollection = blkReference.AttributeCollection;
                            foreach (ObjectId attr in attIdCollection)
                            {
                                AttributeReference attRef = trans.GetObject(attr, OpenMode.ForWrite, false, true) as AttributeReference;
                                if (attRef.Tag.ToUpper().StartsWith("PUBLISH_DATE"))
                                {
                                    using (attRef)
                                    {
                                        // attRef.UpgradeOpen();
                                        attRef.TextString = DateTime.Now.ToShortDateString();
                                        //attRef.DowngradeOpen();
                                    }
                                }
                            }
                        }
                    }
                }
                trans.Commit();
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.Regen();
            }
        }

        private void EnableDisableControls(bool isEnable, bool isLoad = false)
        {
            //Cursor = !isEnable && !isLoad ? Cursors.Wait : Cursors.Arrow;
            btnAdd.IsEnabled = isEnable;
            btnAddAll.IsEnabled = isEnable;
            btnRemove.IsEnabled = isEnable;
            btnRemoveAll.IsEnabled = isEnable;
            if (isLoad)
                menuFile.IsEnabled = isLoad;
            else
                menuFile.IsEnabled = isEnable;
            treeViewDocs.IsEnabled = isEnable;
            //lstBxDrawings.IsEnabled = isEnable;
            //btnPublish.IsEnabled = isLoad ? false : true;
            //btnCancel.IsEnabled = isLoad ? false : true;
            //btnCancel.IsEnabled = false;
            chkCreateDWF.IsEnabled = isEnable;
            chkCreatePDF.IsEnabled = isEnable;
            chkUpdateSearchInfo.IsEnabled = isEnable;
            chkUpdatePublishDate.IsEnabled = isEnable;
            chkDetailedLog.IsEnabled = isEnable;
        }

        private void checkFile(string outputFileName)
        {
            bool loop = true;
            while (loop)
            {
                if (File.Exists(outputFileName))
                {
                    loop = false;
                }
            }
        }

        #region Initiate AutoCAD

        private string _autocadClassId = "AutoCAD.Application";

        //public void EnsureAutoCadIsRunning(string classId)
        //{
        //    if (!string.IsNullOrEmpty(classId) && classId != _autocadClassId)
        //        _autocadClassId = classId;
        //    if (_application == null)
        //    {
        //        try
        //        {
        //            GetAutoCAD();
        //        }
        //        catch (COMException ex)
        //        {
        //            try
        //            {
        //                StartAutoCad();
        //                System.Threading.Thread.Sleep(3000);
        //            }
        //            catch (Exception e2x)
        //            {
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //        }
        //    }
        //}

        //private void StartAutoCad()
        //{
        //    var t = Type.GetTypeFromProgID(_autocadClassId, true);
        //    // Create a new instance Autocad.
        //    var obj = Activator.CreateInstance(t, true);
        //    // No need for casting with dynamics
        //    _application = (AcadApplication)obj;
        //}

        //private void GetAutoCAD()
        //{
        //    _application = (AcadApplication)Marshal.GetActiveObject(_autocadClassId);
        //}

        #endregion

        private void btnCancel_Click_1(object sender, RoutedEventArgs e)
        {
            ClsProperties.IsCancelled = true;
            //EnableDisableControls(true);
            ProgressHeaderPublisher.PublishData("Cancel in progress...");
            //ProgressStatusPublisher.PublishData(" ");
            btnPublish.IsEnabled = true;
            btnCancel.IsEnabled = false;
        }

        private void btnOptions_Click_1(object sender, RoutedEventArgs e)
        {
            Options options = new Options();
            options.ShowDialog();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //SOPSDataTableAdapters.ASSET_EXTENTTableAdapter assetExtentAdapetr = new SOPSDataTableAdapters.ASSET_EXTENTTableAdapter();
                //SOPSData.ASSET_EXTENTDataTable ASSET_EXTENT = new SOPSData.ASSET_EXTENTDataTable();
                //ASSET_EXTENT = assetExtentAdapetr.GetData();
                //ASSET_EXTENTDataGridView.ItemsSource = ASSET_EXTENT;

                EnableDisableControls(false, true);
                chkCreatePDF.IsChecked = BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreatedPDFChecked;
                chkCreateDWF.IsChecked = BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreateDWFChecked;
                chkUpdateSearchInfo.IsChecked = BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdateSearchInfoChecked;
                chkUpdatePublishDate.IsChecked = BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdatePublishDateChecked;

                if (!string.IsNullOrWhiteSpace(sopsDwgLocation))
                {
                    if (!Directory.Exists(sopsDwgLocation))
                        return;
                    string[] files = Directory.GetFiles(sopsDwgLocation);
                    if (files == null || files.Count() == 0)
                        return;
                    foreach (var file in files)
                    {
                        if (!Path.GetExtension(file).Equals(".dwg", StringComparison.InvariantCultureIgnoreCase))
                            continue;
                        ListBoxItem treeViewItem = new ListBoxItem();
                        treeViewItem.Tag = file;
                        treeViewItem.Content = System.IO.Path.GetFileNameWithoutExtension(file);
                        var isExists = treeViewDocs.Items.OfType<ListBoxItem>().ToList().Where(y => y.Content.Equals(treeViewItem.Content)).Select(y => y).FirstOrDefault() != null ? true : false;
                        if (!isExists)
                            treeViewDocs.Items.Add(treeViewItem);
                    }
                    treeViewDocs.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("Content", System.ComponentModel.ListSortDirection.Ascending));
                    EnableDisableControls(true);
                }
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        private void InitializePublishers()
        {
            ProgressBarMaximumPublisher = new Publisher<double>();
            ProgressValuePublisher = new Publisher<double>();
            ProgressStatusPublisher = new Publisher<string>();
            ProgressHeaderPublisher = new Publisher<string>();
        }

        private void chkCreateDWF_Click(object sender, RoutedEventArgs e)
        {
            BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreateDWFChecked = (bool)chkCreateDWF.IsChecked;
            //IniFile iniFile = new IniFile(settingsFilePath);
            //iniFile.IniWriteValue("ConnectionString", "CREATE_DWF", (bool)chkCreateDWF.IsChecked ? "ON" : "OFF");
        }

        private void chkCreatePDF_Click(object sender, RoutedEventArgs e)
        {
            BatchPlottingProperties.BatchPlottingPropertiesInstance.isCreatedPDFChecked = (bool)chkCreatePDF.IsChecked;
            //IniFile iniFile = new IniFile(settingsFilePath);
            //iniFile.IniWriteValue("ConnectionString", "CREATE_PDF", (bool)chkCreatePDF.IsChecked ? "ON" : "OFF");
        }

        private void chkUpdateSearchInfo_Click(object sender, RoutedEventArgs e)
        {
            BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdateSearchInfoChecked = (bool)chkUpdateSearchInfo.IsChecked;
            //IniFile iniFile = new IniFile(settingsFilePath);
            //iniFile.IniWriteValue("ConnectionString", "UPDATE_SEARCH_INFO", (bool)chkUpdateSearchInfo.IsChecked ? "ON" : "OFF");
        }

        private void chkUpdatePublishDate_Click(object sender, RoutedEventArgs e)
        {
            BatchPlottingProperties.BatchPlottingPropertiesInstance.isUpdatePublishDateChecked = (bool)chkUpdatePublishDate.IsChecked;
            //IniFile iniFile = new IniFile(settingsFilePath);
            //iniFile.IniWriteValue("ConnectionString", "UPDATE_PUBLISH_DATE", (bool)chkUpdatePublishDate.IsChecked ? "ON" : "OFF");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ClsProperties.IsFormClosed = true;
        }

        private void chkDetailedLog_Click(object sender, RoutedEventArgs e)
        {
            _detailedLog = (bool)chkDetailedLog.IsChecked ? true : false;
            //IniFile iniFile = new IniFile(settingsFilePath);
            //iniFile.IniWriteValue("ConnectionString", "DETAILED_LOG", _detailedLog ? "ON" : "OFF");
        }

        private void btnAscending_Click(object sender, RoutedEventArgs e)
        {
            //if (!_isSortingOn)
            //{
            //    MessageBox.Show("Settings turned off.", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
            //    return;
            //}
            treeViewDocs.SelectedItems.OfType<ListBoxItem>().ToList().ForEach(x =>
            {
                x.IsSelected = false;
            });
            treeViewDocs.Items.SortDescriptions.Clear();
            treeViewDocs.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("Content", System.ComponentModel.ListSortDirection.Ascending));

        }

        private void btnDescending_Click(object sender, RoutedEventArgs e)
        {
            //if (!_isSortingOn)
            //{
            //    MessageBox.Show("Settings turned off.", "Batch Plotting", MessageBoxButton.OK, MessageBoxImage.Information);
            //    return;
            //}
            treeViewDocs.SelectedItems.OfType<ListBoxItem>().ToList().ForEach(x =>
            {
                x.IsSelected = false;
            });
            treeViewDocs.Items.SortDescriptions.Clear();
            treeViewDocs.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("Content", System.ComponentModel.ListSortDirection.Descending));
        }

        private void Publisher_HeaderDataPublisher(object sender, BatchPlotingPublisher.MessageArgument<string> e)
        {
            lblHeader.Dispatcher.Invoke(() => { lblHeader.Content = e.Message; });
            DoEventsHandler.DoEvents();
        }

        private void Publisher_StatusDataPublisher(object sender, BatchPlotingPublisher.MessageArgument<string> e)
        {
            lblStatusPublisher.Dispatcher.Invoke(() => { lblStatusPublisher.Content = e.Message; });
            DoEventsHandler.DoEvents();
        }

        private void Publisher_ValueDataPublisher(object sender, BatchPlotingPublisher.MessageArgument<double> e)
        {
            pbStatus.Dispatcher.Invoke(() => { pbStatus.Value = pbStatus.Value + e.Message; });
            txtPercentage.Dispatcher.Invoke(() =>
            {
                var value = Convert.ToInt32((pbStatus.Value / pbStatus.Maximum) * 100);
                txtPercentage.Text = value + "%";
            });
            DoEventsHandler.DoEvents();
        }

        private void Publisher_MaximumDataPublisher(object sender, BatchPlotingPublisher.MessageArgument<double> e)
        {
            pbStatus.Dispatcher.Invoke(() => { pbStatus.Value = 0; });
            pbStatus.Dispatcher.Invoke(() => { pbStatus.Maximum = e.Message; });
            DoEventsHandler.DoEvents();
        }

        private void treeViewDocs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (treeViewDocs.SelectedItem != null)
            {
                if (!lstBxDrawings.Items.OfType<ListViewItem>().ToList().Any(x => x.Content.Equals(((ListBoxItem)treeViewDocs.SelectedItem).Content)))
                {
                    ListViewItem lstItem = new ListViewItem();
                    lstItem.Content = ((ListBoxItem)treeViewDocs.SelectedItem).Content;
                    lstItem.Tag = ((ListBoxItem)treeViewDocs.SelectedItem).Tag;
                    lstBxDrawings.Items.Add(lstItem);
                }
            }
        }
    }

}

