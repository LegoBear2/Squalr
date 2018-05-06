﻿namespace Squalr.Source.Scanning
{
    using GalaSoft.MvvmLight.CommandWpf;
    using Squalr.Engine;
    using Squalr.Engine.DataTypes;
    using Squalr.Engine.Logging;
    using Squalr.Engine.Scanning.Scanners;
    using Squalr.Engine.Scanning.Scanners.Constraints;
    using Squalr.Engine.Scanning.Snapshots;
    using Squalr.Engine.Snapshots;
    using Squalr.Engine.Utils.DataStructures;
    using Squalr.Source.Docking;
    using Squalr.Source.Results;
    using Squalr.Source.Tasks;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    /// <summary>
    /// View model for the Manual Scanner.
    /// </summary>
    internal class ManualScannerViewModel : ToolViewModel, IResultDataTypeObserver
    {
        /// <summary>
        /// Singleton instance of the <see cref="ManualScannerViewModel" /> class.
        /// </summary>
        private static Lazy<ManualScannerViewModel> manualScannerViewModelInstance = new Lazy<ManualScannerViewModel>(
                () => { return new ManualScannerViewModel(); },
                LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// The current scan constraint.
        /// </summary>
        private ScanConstraint currentScanConstraint;

        /// <summary>
        /// Collection of all active scan constraints.
        /// </summary>
        private ScanConstraintCollection scanConstraintCollection;

        /// <summary>
        /// Prevents a default instance of the <see cref="ManualScannerViewModel" /> class from being created.
        /// </summary>
        private ManualScannerViewModel() : base("Manual Scanner")
        {
            this.StartScanCommand = new RelayCommand(() => Task.Run(() => this.StartScan()), () => true);

            // Note: Not async to avoid updates slower than the perception threshold
            this.UpdateActiveValueCommand = new RelayCommand<Object>((newValue) => this.UpdateActiveValue(newValue), (newValue) => true);
            this.SelectChangedCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.Changed), () => true);
            this.SelectDecreasedCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.Decreased), () => true);
            this.SelectDecreasedByXCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.DecreasedByX), () => true);
            this.SelectEqualCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.Equal), () => true);
            this.SelectGreaterThanCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.GreaterThan), () => true);
            this.SelectGreaterThanOrEqualCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.GreaterThanOrEqual), () => true);
            this.SelectIncreasedCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.Increased), () => true);
            this.SelectIncreasedByXCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.IncreasedByX), () => true);
            this.SelectLessThanCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.LessThan), () => true);
            this.SelectLessThanOrEqualCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.LessThanOrEqual), () => true);
            this.SelectNotEqualCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.NotEqual), () => true);
            this.SelectUnchangedCommand = new RelayCommand(() => this.ChangeScanConstraintSelection(ScanConstraint.ConstraintType.Unchanged), () => true);

            // Note: Constraint modifying commands cannot be async since they modify the observable collection, which must be done on the same thread as the GUI
            this.AddCurrentConstraintCommand = new RelayCommand(() => this.AddCurrentConstraint(), () => true);
            this.RemoveConstraintCommand = new RelayCommand<ScanConstraint>((ScanConstraint) => this.RemoveConstraint(ScanConstraint), (ScanConstraint) => true);
            this.EditConstraintCommand = new RelayCommand<ScanConstraint>((ScanConstraint) => this.EditConstraint(ScanConstraint), (ScanConstraint) => true);
            this.ClearConstraintsCommand = new RelayCommand(() => this.ClearConstraints(), () => true);
            this.CurrentScanConstraint = new ScanConstraint(ScanConstraint.ConstraintType.Equal);
            this.ScanConstraintCollection = new ScanConstraintCollection();
            this.ScanConstraintCollection.SetElementType(DataType.Int32);

            Task.Run(() => ScanResultsViewModel.GetInstance().Subscribe(this));
            DockingViewModel.GetInstance().RegisterViewModel(this);
        }

        /// <summary>
        /// Gets the command begin the scan.
        /// </summary>
        public ICommand StartScanCommand { get; private set; }

        /// <summary>
        /// Gets the command to update the value of the active scan constraint.
        /// </summary>
        public ICommand UpdateActiveValueCommand { get; private set; }

        /// <summary>
        /// Gets the command to add the current constraint to the list of scan constraints.
        /// </summary>
        public ICommand AddCurrentConstraintCommand { get; private set; }

        /// <summary>
        /// Gets the command to remove the target constraint to the list of scan constraints.
        /// </summary>
        public ICommand RemoveConstraintCommand { get; private set; }

        /// <summary>
        /// Gets the command to edit the target constraint.
        /// </summary>
        public ICommand EditConstraintCommand { get; private set; }

        /// <summary>
        /// Gets the command to clear all added constraints.
        /// </summary>
        public ICommand ClearConstraintsCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.Changed"/> constraint.
        /// </summary>
        public ICommand SelectChangedCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.Decreased"/> constraint.
        /// </summary>
        public ICommand SelectDecreasedCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.DecreasedByX"/> constraint.
        /// </summary>
        public ICommand SelectDecreasedByXCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.Equal"/> constraint.
        /// </summary>
        public ICommand SelectEqualCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.GreaterThan"/> constraint.
        /// </summary>
        public ICommand SelectGreaterThanCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.GreaterThanOrEqual"/> constraint.
        /// </summary>
        public ICommand SelectGreaterThanOrEqualCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.Increased"/> constraint.
        /// </summary>
        public ICommand SelectIncreasedCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.IncreasedByX"/> constraint.
        /// </summary>
        public ICommand SelectIncreasedByXCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.LessThan"/> constraint.
        /// </summary>
        public ICommand SelectLessThanCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.LessThanOrEqual"/> constraint.
        /// </summary>
        public ICommand SelectLessThanOrEqualCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.NotEqual"/> constraint.
        /// </summary>
        public ICommand SelectNotEqualCommand { get; private set; }

        /// <summary>
        /// Gets the command to select the <see cref="ConstraintType.Unchanged"/> constraint.
        /// </summary>
        public ICommand SelectUnchangedCommand { get; private set; }

        /// <summary>
        /// Gets the current set of scan constraints added to the manager.
        /// </summary>
        public FullyObservableCollection<ScanConstraint> Constraints
        {
            get
            {
                return this.ScanConstraintCollection.ValueConstraints;
            }
        }

        /// <summary>
        /// Gets or sets the collection of all active scan constraints.
        /// </summary>
        public ScanConstraintCollection ScanConstraintCollection
        {
            get
            {
                return this.scanConstraintCollection;
            }

            set
            {
                this.scanConstraintCollection = value;
                this.RaisePropertyChanged(nameof(this.ScanConstraintCollection));
            }
        }

        /// <summary>
        /// Gets the current scan constraint, wrapped as a collection for easier display.
        /// </summary>
        public FullyObservableCollection<ScanConstraint> ActiveScanConstraint
        {
            get
            {
                return new FullyObservableCollection<ScanConstraint>() { this.CurrentScanConstraint };
            }
        }

        /// <summary>
        /// Gets a value indicating if the current scan constraint requires a value.
        /// </summary>
        public Boolean IsActiveScanConstraintValued
        {
            get
            {
                return CurrentScanConstraint == null ? true : ScanConstraint.IsValuedConstraint(this.CurrentScanConstraint.Constraint);
            }
        }

        /// <summary>
        /// Gets or sets the current scan constraint.
        /// </summary>
        public ScanConstraint CurrentScanConstraint
        {
            get
            {
                return this.currentScanConstraint;
            }

            set
            {
                this.currentScanConstraint = value;
                this.UpdateAllProperties();
            }
        }

        /// <summary>
        /// Gets a singleton instance of the <see cref="ManualScannerViewModel"/> class.
        /// </summary>
        /// <returns>A singleton instance of the class.</returns>
        public static ManualScannerViewModel GetInstance()
        {
            return ManualScannerViewModel.manualScannerViewModelInstance.Value;
        }

        /// <summary>
        /// Updates the active type.
        /// </summary>
        /// <param name="activeType">The new active type.</param>
        public void Update(DataType activeType)
        {
            // Create a temporary manager to update our current constraint
            ScanConstraintCollection scanConstraintCollection = new ScanConstraintCollection();
            scanConstraintCollection.AddConstraint(this.CurrentScanConstraint);
            scanConstraintCollection.SetElementType(activeType);

            this.ScanConstraintCollection.SetElementType(activeType);
            this.UpdateAllProperties();
        }

        /// <summary>
        /// Starts the scan using the current constraints.
        /// </summary>
        private void StartScan()
        {
            // Create a constraint manager that includes the current active constraint
            ScanConstraintCollection allScanConstraints = this.ScanConstraintCollection.Clone();
            allScanConstraints.AddConstraint(this.CurrentScanConstraint);

            if (!allScanConstraints.IsValid())
            {
                Logger.Log(LogLevel.Warn, "Unable to start scan with given constraints");
                return;
            }

            // Collect values
            TrackableTask<Snapshot> valueCollectorTask = ValueCollector.CollectValues(
                SnapshotManager.GetSnapshot(Snapshot.SnapshotRetrievalMode.FromActiveSnapshotOrPrefilter),
                DataType.Int32);

            TaskTrackerViewModel.GetInstance().TrackTask(valueCollectorTask);

            // Perform manual scan on value collection complete
            valueCollectorTask.OnCompletedEvent += ((completedValueCollection) =>
            {
                Snapshot values = completedValueCollection.Result;
                TrackableTask<Snapshot> scanTask = ManualScanner.Scan(
                    values,
                    DataType.Int32,
                    allScanConstraints);

                TaskTrackerViewModel.GetInstance().TrackTask(scanTask);
                SnapshotManager.SaveSnapshot(scanTask.Result);
            });
        }

        /// <summary>
        /// Adds the current constraint to the list of scan constraints.
        /// </summary>
        private void AddCurrentConstraint()
        {
            this.ScanConstraintCollection.AddConstraint(this.CurrentScanConstraint);
            this.CurrentScanConstraint = new ScanConstraint(this.CurrentScanConstraint.Constraint, this.CurrentScanConstraint.ConstraintValue);
            this.UpdateAllProperties();
        }

        /// <summary>
        /// Updates the value of the current scan constraint.
        /// </summary>
        /// <param name="newValue">The new value of the scan constraint.</param>
        private void UpdateActiveValue(Object newValue)
        {
            this.CurrentScanConstraint.ConstraintValue = newValue;
            this.UpdateAllProperties();
        }

        /// <summary>
        /// Edits the target constraint by removing it and making it the current constraint.
        /// </summary>
        /// <param name="scanConstraint">The constraint to edit.</param>
        private void EditConstraint(ScanConstraint scanConstraint)
        {
            this.ScanConstraintCollection.RemoveConstraints(scanConstraint);
            this.CurrentScanConstraint = scanConstraint;
            this.UpdateAllProperties();
        }

        /// <summary>
        /// Removes the target constraint from the list of scan constraints.
        /// </summary>
        /// <param name="scanConstraint">The constraint to remove.</param>
        private void RemoveConstraint(ScanConstraint scanConstraint)
        {
            this.ScanConstraintCollection.RemoveConstraints(scanConstraint);
            this.UpdateAllProperties();
        }

        /// <summary>
        /// Clears all scan constraints.
        /// </summary>
        private void ClearConstraints()
        {
            this.ScanConstraintCollection.ClearConstraints();
            this.UpdateAllProperties();
        }

        /// <summary>
        /// Changes the current scan constraint.
        /// </summary>
        /// <param name="constraint">The new scan constraint.</param>
        private void ChangeScanConstraintSelection(ScanConstraint.ConstraintType constraint)
        {
            this.CurrentScanConstraint.Constraint = constraint;
            this.UpdateAllProperties();
        }

        /// <summary>
        /// Raises property changed events for all used properties. This is convenient since there are several interdependencies between these.
        /// </summary>
        private void UpdateAllProperties()
        {
            this.RaisePropertyChanged(nameof(this.CurrentScanConstraint));
            this.RaisePropertyChanged(nameof(this.ActiveScanConstraint));
            this.RaisePropertyChanged(nameof(this.IsActiveScanConstraintValued));
        }
    }
    //// End class
}
//// End namespace