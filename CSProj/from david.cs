using Grasshopper.Kernel;


public class FSComponent : GH_Component, IGH_VariableParameterComponent
{

	public FSComponent() : base("File Reader", "FileR", "Read a collection of files", "FS", "FS")
	{
	}

	public override System.Guid ComponentGuid {
		get { return new Guid("{DA0FBD61-7D2B-4DF3-9AB9-C5DD1F237560}"); }
	}

	protected override void RegisterInputParams(Kernel.GH_Component.GH_InputParamManager pManager)
	{
		pManager.AddTextParameter("Folder", "F", "Folder to watch", GH_ParamAccess.item);
		pManager.AddTextParameter("Pattern", "P", "File name pattern to watch", GH_ParamAccess.item, "*.txt");

		pManager(1).Optional = true;
	}

	protected override void RegisterOutputParams(Kernel.GH_Component.GH_OutputParamManager pManager)
	{
	}

	protected override void SolveInstance(Kernel.IGH_DataAccess DA)
	{
		//The first time we run, we need to specifically call FileChanged to trigger synching.
		if ((m_firstTime)) {
			m_firstTime = false;
			FileChanged(null);
		}

		//We only allow one folder and one pattern at the moment.
		if ((DA.Iteration > 0)) {
			AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "This component can only handle a single folder and pattern");
			return;
		}

		//Destroy the watcher.
		if ((m_watcher != null)) {
			m_watcher.Dispose();
			m_watcher = null;
		}

		//Get new data.
		m_folder = null;
		m_pattern = null;

		if ((!DA.GetData(0, m_folder)))
			return;
		DA.GetData(1, m_pattern);

		//Repair the pattern in case of null.
		if ((string.IsNullOrEmpty(m_pattern))) {
			m_pattern = "*";
		}

		//Create a new watcher.
		m_watcher = GH_FileWatcher.CreateDirectoryWatcher(m_folder, m_pattern, GH_FileWatcherEvents.All, FileChanged);

		//At this point our output params ought to be correct.
		//Get all file data and assignt to outputs.
		string[] data = GetAllFileData(m_folder, m_pattern);
		if ((data == null))
			return;

		for (Int32 i = 0; i <= Math.Min(data.Length, Params.Output.Count) - 1; i++) {
			DA.SetData(i, data(i));
		}
	}

	#region "File System Events"
	private bool m_firstTime = true;
	private string m_folder;
	private string m_pattern;

	private GH_FileWatcher m_watcher;
	private void FileChanged(string filename)
	{
		GH_Document doc = OnPingDocument();
		if ((doc == null)) {
			UpdateParameters(doc);
			return;
		}

		if ((doc.SolutionState == GH_ProcessStep.Process)) {
			//We're currenty inside a solution, we cannot call UpdateParameters immediately.
			doc.ScheduleSolution(1, UpdateParameters);
		} else {
			if ((!UpdateParameters(doc) && filename != null)) {
				ExpireSolution(true);
			}
		}
	}
	private bool UpdateParameters(GH_Document doc)
	{
		//This is the hard bit, we need to make sure our output parameters
		//are matched up with the files in the folder.
		//This is also where we ought to try and do our best to not 
		//nuke any User Defined wires. At the very least this means
		//maintaining existing wires for existing files.

		//First check if we're allowed to modify outputs.
		if ((doc != null)) {
			if ((doc.SolutionState == GH_ProcessStep.Process)) {
				//We're up shit creek without a paddle now, just abort to prevent worse.
				return false;
			}
		}

		//Step 1. get all the files we need to cater for.
		string[] files = GetAllFileNames(m_folder, m_pattern);

		//Step 1b. let's make sure we actually need to do something.
		bool outOfSync = true;
		if ((files.Length == Params.Output.Count)) {
			outOfSync = false;

			for (Int32 i = 0; i <= files.Length - 1; i++) {
				string fileName = IO.Path.GetFileNameWithoutExtension(files(i));
				string paramName = Params.Output(i).NickName;
				if ((!fileName.Equals(paramName, StringComparison.OrdinalIgnoreCase))) {
					//Ok, we're now officially out of synch.
					outOfSync = true;
					break; // TODO: might not be correct. Was : Exit For
				}
			}
		}
		if ((!outOfSync))
			return false;

		//Step 2. cache all existing parameters.
		List<IGH_Param> existingParams = new List<IGH_Param>(Params.Output);

		//Step 3. create a sync object for cleanup.
		object sync = Params.EmitSyncObject;

		//Step 4. remove all parameters manually, this is naughty, normally you'd call Params.UnregisterOutputParameter()
		Params.Output.Clear();

		//Step 5. recreate all parameters.
		foreach (string file in files) {
			string fileName = IO.Path.GetFileNameWithoutExtension(file);
			IGH_Param fileParam = null;

			//First, we need to check whether a parameter pointing at this file used to exist.
			//If that's the case, recycle it.
			foreach (IGH_Param oldParam in existingParams) {
				if ((oldParam.NickName.Equals(fileName, StringComparison.OrdinalIgnoreCase))) {
					fileParam = oldParam;
					existingParams.Remove(oldParam);
					break; // TODO: might not be correct. Was : Exit For
				}
			}

			if ((fileParam == null)) {
				//It would seem there was no parameter for this file, create a new one.
				fileParam = new Parameters.Param_String();
				fileParam.Name = "File: " + fileName;
				fileParam.NickName = fileName;
			}

			Params.RegisterOutputParam(fileParam);
		}

		//Step 6. use the sync object to perform cleanup on all lost parameters.
		Params.Sync(sync);

		//Step 7. make sure everyone knows we've just been naughty.
		Params.OnParametersChanged();

		//Step 8. invoke the cleanup code.
		VariableParameterMaintenance();

		//Step 9. invoke a new solution.
		ExpireSolution(true);
		return true;
	}

	private string[] GetAllFileNames(string folder, string pattern)
	{
		try {
			return IO.Directory.GetFiles(folder, pattern);
		} catch (Exception ex) {
			return null;
		}
	}
	private string[] GetAllFileData(string folder, string pattern)
	{
		try {
			string[] files = GetAllFileNames(folder, pattern);
			if ((files == null))
				return null;
			if ((files.Length == 0))
				return null;

			string[] content = null;
			 // ERROR: Not supported in C#: ReDimStatement


			for (Int32 i = 0; i <= files.Length - 1; i++) {
				content(i) = IO.File.ReadAllText(files(i));
			}

			return content;
		} catch (Exception ex) {
			return null;
		}
	}
	#endregion

	#region "IGH_VariableParameterComponent implementation"
	//We implement this interface so it helps us out with (de)serializing.
	//We just need to implement a non-functional version.

	public bool CanInsertParameter(Kernel.GH_ParameterSide side, int index)
	{
		return false;
	}
	public bool CanRemoveParameter(Kernel.GH_ParameterSide side, int index)
	{
		return false;
	}
	public Kernel.IGH_Param CreateParameter(Kernel.GH_ParameterSide side, int index)
	{
		return null;
	}
	public bool DestroyParameter(Kernel.GH_ParameterSide side, int index)
	{
		return false;
	}
	public void VariableParameterMaintenance()
	{
		//We'll add some logic here where we'll make all output parameter nicknamed unmutable.
		foreach (IGH_Param param in Params.Output) {
			param.MutableNickName = false;
		}
	}
	#endregion
}
