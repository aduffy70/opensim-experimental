/*
 * Copyright (c) Contributors http://github.com/aduffy70/vMeadow
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the vMeadow Module nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Timers;

using log4net;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Mono.Addins;

[assembly: Addin("vMeadowModule", "0.1")]
[assembly: AddinDependency("OpenSim", "0.5")]
namespace vMeadowModule
{
    [Extension(Path="/OpenSim/RegionModules",NodeName="RegionModule")]
    public class vMeadowModule : INonSharedRegionModule, IVegetationModule
    {
        //Set up logging and dialog messages
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        IDialogModule m_dialogmod;
        //Configurable settings
        bool m_enabled = false;
        int m_channel;  //Channel for chat commands
        int m_cycleTime; //Time in milliseconds between cycles
        float m_xPosition; //inworld x coordinate for the 0,0 cell position
        float m_yPosition;  //inworld y coordinate for the 0,0 cell position
        int m_xCells;
        int m_yCells;
        int m_generations; //Number of generations to simulate
        float m_cellSpacing; //Space in meters between cell positions
        bool m_naturalAppearance; //Whether the plants are placed in neat rows or randomized a bit
        string m_configPath; //Url path to community config settings
        string m_logPath; //Local path to folder where logs will be stored
        string m_instanceTag; //Unique identifier for logs from this region
        SceneObjectGroup[,] m_prims; //list of objects managed by this module
        int[,,] m_cellStatus; // plant type for each cell in each generation [gen,x,y]
        string[] m_omvTrees = new string[22] {"None", "Pine1", "Pine2", "WinterPine1", "WinterPine2", "Oak", "TropicalBush1", "TropicalBush2", "Palm1", "Palm2", "Dogwood", "Cypress1", "Cypress2", "Plumeria", "WinterAspen", "Eucalyptus", "Fern", "Eelgrass", "SeaSword", "BeachGrass1", "Kelp1", "Kelp2"};

        int[] m_communityMembers = new int[6] {0, 1, 2, 5, 16, 18}; //Default plants to include in the community
        bool m_isRunning = false; //Keep track of whether the visualization is running
        bool m_isSimulated = false; //Whether a simulation has been run.
        Timer m_cycleTimer = new Timer(); //Timer to replace the region heartbeat
        Timer m_pauseTimer = new Timer(); //Timer to delay trying to delete objects before the region has loaded completely
        Scene m_scene;
        Random m_random = new Random(); //A Random Class object to use throughout this module
        //Replacement Matrix.  The probability of replacement of one species by a surrounding species.  Example [0,1] is the probability that species 1 will be replaced by species 0, if all 8 of species 1's neighbors are species 0.
        /*//Values from Thorhallsdottir 1990 as presented by Silvertown et al 1992.
        float[,] m_replacementMatrix = new float[6,6] {{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f},
                                                       {0.00f, 0.00f, 0.02f, 0.06f, 0.05f, 0.03f},
                                                       {0.00f, 0.23f, 0.00f, 0.09f, 0.32f, 0.37f},
                                                       {0.00f, 0.06f, 0.08f, 0.00f, 0.16f, 0.09f},
                                                       {0.00f, 0.44f, 0.06f, 0.06f, 0.00f, 0.11f},
                                                       {0.00f, 0.03f, 0.02f, 0.03f, 0.05f, 0.00f}};*/
        float[,] m_replacementMatrix = new float[6,6] {{0f, 0f, 0f, 0f, 0f, 0f},
                                                       {0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f},
                                                       {0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f},
                                                       {0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f},
                                                       {0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f},
                                                       {0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f}};

        //TODO: These need to come from the webtool!
        int[] m_ageMaximum = new int[6] {0, 10, 10, 10, 10, 10}; //Maximum age for each species
        //Optimal values and shape parameters for each species
        float[] m_altitudeOptimal = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_altitudeShape = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_salinityOptimal = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_salinityShape = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_drainageOptimal = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_drainageShape = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_fertilityOptimal = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_fertilityShape = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};

        int m_currentGeneration = 0; //The currently displayed generation
        bool m_isReverse = false; //Whether we are stepping backward through the simulation
        int[,] m_displayedPlants; //Tracks the currently displayed plants
        int[] m_speciesCounts = new int[6] {0, 0, 0, 0, 0, 0}; //Tracks species counts so we can compare acrossed generations
        int[,] m_totalSpeciesCounts; //Total species counts for each generation.  TODO: This will replace m_speciesCounts.  We will calculate this during the simulation instead of the visualization since it is needed for the replacement probability calculations.
        Vector3[,] m_coordinates; //Keeps track of the region coordinates and groundlevel where each plant will be placed so we only have to calculate them once.


        #region INonSharedRegionModule interface

        public void Initialise(IConfigSource config)
        {
            IConfig vMeadowConfig = config.Configs["vMeadow"];
            if (vMeadowConfig != null)
            {
                m_enabled = vMeadowConfig.GetBoolean("enabled", false);
                m_cycleTime = vMeadowConfig.GetInt("cycle_time", 10) * 1000;
                m_channel = vMeadowConfig.GetInt("chat_channel", 18);
                m_xCells = vMeadowConfig.GetInt("x_cells", 36);
                m_yCells = vMeadowConfig.GetInt("y_cells", 36);
                m_xPosition = vMeadowConfig.GetFloat("x_position", 128);
                m_yPosition = vMeadowConfig.GetFloat("y_position", 128);
                m_cellSpacing = vMeadowConfig.GetFloat("cell_spacing", 2);
                m_naturalAppearance = vMeadowConfig.GetBoolean("natural_appearance", true);
                m_configPath = vMeadowConfig.GetString("config_path", "http://fernseed.usu.edu/vMeadowInfo/");
                m_generations = vMeadowConfig.GetInt("generations", 10000);
                m_logPath = vMeadowConfig.GetString("log_path", "addon-modules/vMeadow/logs/");
                m_instanceTag = vMeadowConfig.GetString("instance_tag", "myregion");
            }
            if (m_enabled)
            {
                m_log.Info("[vMeadowModule] Initializing...");
            }
        }

        public void AddRegion(Scene scene)
        {
            if (m_enabled)
            {
                m_scene = scene;
                m_dialogmod = m_scene.RequestModuleInterface<IDialogModule>();
                m_pauseTimer.Elapsed += new ElapsedEventHandler(OnPauseTimer);
                m_pauseTimer.Interval = 30000;
                m_prims = new SceneObjectGroup[m_xCells, m_yCells];
                RandomizeStartMatrix();
                m_pauseTimer.Start(); //Don't allow users to setup or use module til all objects have time to load from datastore
            }
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get
            {
                return "vMeadowModule";
            }
        }

        public Type ReplaceableInterface
        {
            get
            {
                return null;
            }
        }

        #endregion

        #region IVegetationModule Members

        public SceneObjectGroup AddTree(UUID uuid, UUID groupID, Vector3 scale, Quaternion rotation, Vector3 position, Tree treeType, bool newTree)
        {
            PrimitiveBaseShape treeShape = new PrimitiveBaseShape();
            treeShape.PathCurve = 16;
            treeShape.PathEnd = 49900;
            treeShape.PCode = newTree ? (byte)PCode.NewTree : (byte)PCode.Tree;
            treeShape.Scale = scale;
            treeShape.State = (byte)treeType;
            return m_scene.AddNewPrim(uuid, groupID, position, rotation, treeShape);
        }

        #endregion

        #region IEntityCreator Members

        protected static readonly PCode[] creationCapabilities = new PCode[] { PCode.Grass, PCode.NewTree, PCode.Tree };

        public PCode[] CreationCapabilities
        {
            get
            {
                return creationCapabilities;
            }
        }

        public SceneObjectGroup CreateEntity(UUID ownerID, UUID groupID, Vector3 pos, Quaternion rot, PrimitiveBaseShape shape)
        {
            if (Array.IndexOf(creationCapabilities, (PCode)shape.PCode) < 0)
            {
                m_log.DebugFormat("[VEGETATION]: PCode {0} not handled by {1}", shape.PCode, Name);
                return null;
            }
            SceneObjectGroup sceneObject = new SceneObjectGroup(ownerID, pos, rot, shape);
            SceneObjectPart rootPart = sceneObject.GetChildPart(sceneObject.UUID);
            rootPart.AddFlag(PrimFlags.Phantom);
            m_scene.AddNewSceneObject(sceneObject, false);
            sceneObject.SetGroup(groupID, null);
            return sceneObject;
        }

        #endregion

        #region Module management or interface-specific functions

        void OnPauseTimer(object source, ElapsedEventArgs e)
        {
            //After region has had time to load all objects from database after a restart...
            //Without this pause on region startup it was possible to try to clear all plants before the plants had been completely loaded from the database.
            m_pauseTimer.Stop();
            m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
            m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
            m_cycleTimer.Elapsed += new ElapsedEventHandler(OnCycleTimer);
            m_cycleTimer.Interval = m_cycleTime;
        }

        void OnChat(Object sender, OSChatMessage chat)
        {
            if (chat.Channel != m_channel)
            {
                //Message is not for this module
                return;
            }
            else if (chat.Message.ToLower() == "reset")
            {
                Alert("Resetting community. This may take a minute...");
                if (m_isRunning)
                {
                    StopVisualization();
                }
                ClearAllPlants();
                Alert("Cleared existing plants...");
                ClearLogs();
                Alert("Cleared logs...");
                RunSimulation();
                Alert("Simulation complete.  Generating plants...");
                VisualizeGeneration(0);
                AlertAndLog("Community reset. Loaded generation 0...");
                m_isSimulated = true;
            }
            else if (chat.Message.ToLower() == "forward")
            {
                if (m_isSimulated)
                {
                    if (!m_isRunning)
                    {
                        Alert("Stepping forward...");
                        StartVisualization(false);
                    }
                    else
                    {
                        AlertAndLog("Already running. Stop first to change direction...");
                    }
                }
                else
                {
                    AlertAndLog("Cannot start.  Please reset the community first...");
                }
            }
            else if (chat.Message.ToLower() == "reverse")
            {
                if (m_isSimulated)
                {
                    if (!m_isRunning)
                    {
                        Alert("Stepping backward...");
                        StartVisualization(true);
                    }
                    else
                    {
                        AlertAndLog("Already running. Stop first to change direction...");
                    }
                }
                else
                {
                    AlertAndLog("Cannot start.  Please reset the community first...");
                }
            }
            else if (chat.Message.ToLower() == "stop")
            {
                if (m_isRunning)
                {
                    Alert("Stopping...");
                    StopVisualization();
                }
                else
                {
                    AlertAndLog("Already stopped...");
                }
            }
            else if (chat.Message.ToLower() == "clear")
            {
                if (m_isRunning)
                {
                    StopVisualization();
                }
                AlertAndLog("Clearing all plants.  This may take a minute...");
                ClearAllPlants();
            }
            else if (chat.Message.ToLower() == "+")
            {
                if (m_isRunning)
                {
                   AlertAndLog("Already running. Stop first...");
                }
                else
                {
                    if (m_currentGeneration < m_generations - 1)
                    {
                        Alert("Advancing one step...");
                        VisualizeGeneration(m_currentGeneration + 1);
                    }
                    else
                    {
                        Alert("Already at the last step...");
                    }
                }
            }
            else if (chat.Message.ToLower() == "-")
            {
                if (m_isRunning)
                {
                   AlertAndLog("Already running. Stop first...");
                }
                else
                {
                    if (m_currentGeneration > 0)
                    {
                        Alert("Backing up one step...");
                        VisualizeGeneration(m_currentGeneration - 1);
                    }
                    else
                    {
                        Alert("Already at step 0...");
                    }
                }
            }
            else if (chat.Message.ToLower() == "now")
            {
                CalculateStatistics(m_currentGeneration, m_speciesCounts, false);
            }
            else if (chat.Message.ToLower() == "test")
            {
                //TEMP: Just a place to plug in temporary test code.

            }
            else if (chat.Message.Length > 5)
            {
                if (chat.Message.ToLower().Substring(0,4) == "step")
                {
                    if (m_isRunning)
                    {
                        AlertAndLog("Already running. Stop first...");
                    }
                    else
                    {
                        try
                        {
                            int generation = Convert.ToInt32(chat.Message.Substring(5));
                            VisualizeGeneration(generation);
                            AlertAndLog(String.Format("Displaying generation {0}...", generation));
                        }
                        catch
                        {
                            AlertAndLog("Invalid generation number...");
                        }
                    }
                }
                else
                {
                    //Try to read configuration info from a url
                    bool readSuccess = ReadConfigs(System.IO.Path.Combine(m_configPath, "data?id=" + chat.Message));
                    if (readSuccess)
                    {
                        if (m_isRunning)
                        {
                            StopVisualization();
                        }
                        ClearAllPlants();
                        Alert("Cleared existing plants...");
                        ClearLogs();
                        Alert("Cleared logs...");
                        RunSimulation();
                        Alert("Simulation complete.  Generating plants...");
                        VisualizeGeneration(0);
                        m_isSimulated = true;
                    }
                }
            }
            else
            {
                //Invalid command
                AlertAndLog("Invalid command...");
            }
        }

        public void Alert(string message)
        {
            if (m_dialogmod != null)
            {
                m_dialogmod.SendGeneralAlert(String.Format("{0}: {1}", Name, message));
            }
        }

        public void AlertAndLog(string message)
        {
            m_log.DebugFormat("[{0}] {1}", Name, message);
            Alert(message);
        }


        #endregion

        #region Visualization-specific functions

        void OnCycleTimer(object source, ElapsedEventArgs e)
        {
            //Stop the timer so we won't have problems if this process isn't finished before the timer event is triggered again.
            m_cycleTimer.Stop();
            int nextGeneration;
            //Advance visualization by a generation
            if (m_isReverse)
            {
                if (m_currentGeneration > 0)
                {
                    nextGeneration = m_currentGeneration - 1;
                }
                else
                {
                    //Stop stepping through the visualization if we can't go back further.
                    m_isRunning = false;
                    AlertAndLog("Reached generation 0.  Stopping...");
                    return;
                }
            }
            else
            {
                if (m_currentGeneration < m_generations - 1)
                {
                    nextGeneration = m_currentGeneration + 1;
                }
                else
                {
                    //Stop stepping through the visualization if we can't go further.
                    m_isRunning = false;
                    AlertAndLog(String.Format("Reached generation {0}.  Stopping...", m_currentGeneration));
                    return;
                }
            }
            VisualizeGeneration(nextGeneration);
            if (m_isRunning)
            {
                //Check that there hasn't been a request to stop the timer before restarting the timer
                m_cycleTimer.Start();
            }
        }

        void CalculateStatistics(int generation, int[] speciesCounts, bool needToLog)
        {
            //TODO: This function should be split up into logical subunits.  It generates two different strings, logs one and sends the other to huds, and modifies a global variable.
            //TODO: This should probably happen during the simulation, not during the visualization.
            string[] hudString = new string[5];
            hudString[0] = String.Format("Generation: {0}", generation);
            hudString[1] = "Species";
            hudString[2] = "Qty";
            hudString[3] = "Change";
            hudString[4] = "%";
            string logString = generation.ToString();
            int totalPlants = speciesCounts[1] + speciesCounts[2] + speciesCounts[3] + speciesCounts[4] + speciesCounts[5];
            for (int i=1; i<6; i++)
            {
                hudString[1] += "\n" + i;
                hudString[2] += "\n" + speciesCounts[i];
                int qtyChange = speciesCounts[i] - m_speciesCounts[i];
                string direction = "";
                if (qtyChange > 0)
                {
                    direction = "+";
                }
                hudString[3] += "\n" + direction + qtyChange;
                float percent;
                if (totalPlants > 0) //Avoid divide-by-zero errors
                {
                    percent = (float)(Math.Round((double)((speciesCounts[i] / (float)totalPlants) * 100), 1));
                }
                else
                {
                    percent = 0f;
                }
                hudString[4] += "\n" + percent + "%";
                logString += String.Format(",{0}", speciesCounts[i]);
            }
            Array.Copy(speciesCounts, m_speciesCounts, 6);
            if (needToLog)
            {
                LogData(logString);
            }
            UpdateHUDs(hudString);
        }

        void ClearAllPlants()
        {
            //Delete all vMeadow plants in the region
            EntityBase[] everyObject = m_scene.GetEntities();
            SceneObjectGroup sog;
            foreach (EntityBase e in everyObject)
            {
                if (e is SceneObjectGroup)
                {
                    sog = (SceneObjectGroup)e;
                    if (sog.RootPart.Name == "vMeadowPlant")
                    {
                        DeletePlant(sog);
                    }
                }
            }
            m_prims = new SceneObjectGroup[m_xCells, m_yCells];
            m_displayedPlants = new int[m_xCells, m_yCells];
        }

        void ClearLogs()
        {
            string logFile = System.IO.Path.Combine(m_logPath, m_instanceTag + "-community.log");
            System.IO.File.Delete(logFile);
        }

        SceneObjectGroup CreatePlant(int xPos, int yPos, int plantTypeIndex)
        {
            //Generates a plant
            //Don't send this function non-plant (0 or -1) plantTypeIndex values or locations outside of the region.
            Tree treeType = (Tree) Enum.Parse(typeof(Tree), m_omvTrees[m_communityMembers[plantTypeIndex]]);
            SceneObjectGroup newPlant = AddTree(UUID.Zero, UUID.Zero, new Vector3(1.0f, 1.0f, 1.0f), Quaternion.Identity, m_coordinates[xPos, yPos], treeType, false);
            newPlant.RootPart.Name = "vMeadowPlant";
            return newPlant;
        }

        void DeletePlant(SceneObjectGroup deadPlant)
        {
            try
            {
                m_scene.DeleteSceneObject(deadPlant, false);
            }
            catch
            {
                m_log.Debug("[vMeadowModule] Tried to delete a non-existent plant! Was it manually removed?");
            }
        }

        void LogData(string logString)
        {
            string logFile = System.IO.Path.Combine(m_logPath, m_instanceTag + "-community.log");
            System.IO.StreamWriter dataLog = System.IO.File.AppendText(logFile);
            dataLog.WriteLine(logString);
            dataLog.Close();
        }

        void StartVisualization(bool isReverse)
        {
            //Start stepping forward through generations of the visualization
            string direction = "forward";
            if (isReverse)
            {
                direction = "backward";
            }
            m_log.Debug("[vMeadowModule] Stepping " + direction + "...");
            m_isRunning = true;
            m_isReverse = isReverse;
            m_cycleTimer.Start();
        }

        void StopVisualization()
        {
            //Stop stepping through generations in the visualization
            m_log.Debug("[vMeadowModule] Stopping...");
            m_isRunning = false;
            m_cycleTimer.Stop();
        }

        void UpdateHUDs(string[] hudString)
        {
            //Display current data on all vpcHUDs in the region
            lock (m_scene)
            {
                EntityBase[] everyObject = m_scene.GetEntities();
                SceneObjectGroup sog;
                foreach (EntityBase e in everyObject)
                {
                    if (e is SceneObjectGroup) //ignore avatars
                    {
                        sog = (SceneObjectGroup)e;
                        if (sog.Name.Length > 9)
                        {
                            //Avoid an error on objects with short names.
                            //HUD must be the correct major release # to work.  If you make changes that will break old huds, update the release number. Minor release numbers track non-breaking HUD changes.
                            if (sog.Name.Substring(0,8) == "vpcHUDv1")
                            {
                                //Use yellow for HUD text.  It shows against sky, water, or land.
                                Vector3 textColor = new Vector3(1.0f, 1.0f, 0.0f);
                                //Place floating text on each named prim of the inworld HUD
                                foreach (SceneObjectPart labeledPart in sog.Parts)
                                {
                                    if (labeledPart.Name == "GenerationvpcHUD")
                                        labeledPart.SetText(hudString[0], textColor, 1.0);
                                    else if (labeledPart.Name == "SpeciesvpcHUD")
                                        labeledPart.SetText(hudString[1], textColor, 1.0);
                                    else if (labeledPart.Name == "QtyvpcHUD")
                                        labeledPart.SetText(hudString[2], textColor, 1.0);
                                    else if (labeledPart.Name == "QtyChangevpcHUD")
                                        labeledPart.SetText(hudString[3], textColor, 1.0);
                                    else if (labeledPart.Name == "PercentvpcHUD")
                                        labeledPart.SetText(hudString[4], textColor, 1.0);
                                    else if (labeledPart.Name == "PercentChangevpcHUD")
                                        labeledPart.SetText(hudString[5], textColor, 1.0);
                                }
                            }
                        }
                    }
                }
            }
        }

        void VisualizeGeneration(int nextGeneration)
        {
            //Update the visualization with plants from the next generation.
            //Don't waste time deleting and replacing plants if the species isn't going to change.
            int [] speciesCounts = new int[6] {0, 0, 0, 0, 0, 0};
            for (int y=0; y<m_yCells; y++)
            {
                for (int x=0; x<m_xCells; x++)
                {
                    int currentSpecies = m_displayedPlants[x, y];
                    int newSpecies = m_cellStatus[nextGeneration, x, y];
                    //Only delete and replace the existing plant if it needs to change
                    if (newSpecies != currentSpecies)
                    {
                        if ((currentSpecies != 0) && (currentSpecies != -1))
                        {
                            //Don't try to delete plants that don't exist
                            DeletePlant(m_prims[x, y]);
                        }
                        if ((newSpecies != 0) && (newSpecies != -1))
                        {
                            m_prims[x, y] = CreatePlant(x, y, newSpecies);
                            if (m_prims[x, y] != null)
                            {
                                m_displayedPlants[x, y] = newSpecies;
                            }
                            else
                            {
                                m_displayedPlants[x, y] = 0;
                            }
                        }
                        else
                        {
                            m_prims[x, y] = null;
                            m_displayedPlants[x, y] = 0;
                        }
                    }
                    if (m_displayedPlants[x, y] != 0)
                    {
                        speciesCounts[newSpecies] += 1;
                    }
                }
            }
            CalculateStatistics(nextGeneration, speciesCounts, true);
            m_currentGeneration = nextGeneration;
        }

        #endregion

        #region Simulation-specific functions

        int[] GetNeighborSpeciesCounts(int x, int y, int rowabove, int rowbelow, int colright, int colleft, int generation)
        {
            //Get counts of neighborspecies
            //At edges, missing neighborTypes are -1
            int[] neighborSpeciesCounts = new int[6] {0, 0, 0, 0, 0, 0};
            int neighborType;
            if (colleft >= 0)
            {
                neighborType = m_cellStatus[generation, colleft, y];
                if (neighborType != -1)
                {
                    neighborSpeciesCounts[neighborType]++;
                }
                if (rowbelow >= 0)
                {
                    neighborType = m_cellStatus[generation, colleft, rowbelow];
                    if (neighborType != -1)
                    {
                        neighborSpeciesCounts[neighborType]++;
                    }
                }
                if (rowabove < m_yCells)
                {
                    neighborType = m_cellStatus[generation, colleft, rowabove];
                    if (neighborType != -1)
                    {
                        neighborSpeciesCounts[neighborType]++;
                    }
                }
            }
            if (colright < m_xCells)
            {
                neighborType = m_cellStatus[generation, colright, y];
                if (neighborType != -1)
                {
                    neighborSpeciesCounts[neighborType]++;
                }
                if (rowbelow >= 0)
                {
                    neighborType = m_cellStatus[generation, colright, rowbelow];
                    if (neighborType != -1)
                    {
                        neighborSpeciesCounts[neighborType]++;
                    }
                }
                if (rowabove < m_yCells)
                {
                    neighborType = m_cellStatus[generation, colright, rowabove];
                    if (neighborType != -1)
                    {
                        neighborSpeciesCounts[neighborType]++;
                    }
                }
            }
            if (rowbelow >= 0)
            {
                neighborType = m_cellStatus[generation, x, rowbelow];
                if (neighborType != -1)
                {
                    neighborSpeciesCounts[neighborType]++;
                }
            }
            if (rowabove < m_yCells)
            {
                neighborType = m_cellStatus[generation, x, rowabove];
                if (neighborType != -1)
                {
                    neighborSpeciesCounts[neighborType]++;
                }
            }
            return neighborSpeciesCounts;
        }

        float[] GetReplacementProbabilities(int currentSpecies, int[] neighborSpeciesCounts, int generation)
        {
            //Calculate the probability that the current plant will be replaced by each species.
            float[] replacementProbabilities = new float[6];
            float totalCount = (float)(m_totalSpeciesCounts[generation, 0] + m_totalSpeciesCounts[generation, 1] + m_totalSpeciesCounts[generation, 2] + m_totalSpeciesCounts[generation, 3] + m_totalSpeciesCounts[generation, 4] + m_totalSpeciesCounts[generation, 5]);
            for (int species=0; species<6; species++)
            {
                replacementProbabilities[species] = ((m_replacementMatrix[species, currentSpecies] * ((float)neighborSpeciesCounts[species] / 8.0f)) * 0.75f) + ((m_replacementMatrix[species, currentSpecies] * ((float)m_totalSpeciesCounts[generation, species] / totalCount)) * 0.25f);
            }
            return replacementProbabilities;
        }

        public float GroundLevel(Vector3 location)
        {
            //Return the ground level at the specified location.
            //The first part of this function performs essentially the same function as llGroundNormal() without having to be called by a prim.
            //Find two points in addition to the position to define a plane
            Vector3 p0 = new Vector3(location.X, location.Y, (float)m_scene.Heightmap[(int)location.X, (int)location.Y]);
            Vector3 p1 = new Vector3();
            Vector3 p2 = new Vector3();
            if ((location.X + 1.0f) >= m_scene.Heightmap.Width)
                p1 = new Vector3(location.X + 1.0f, location.Y, (float)m_scene.Heightmap[(int)location.X, (int)location.Y]);
            else
                p1 = new Vector3(location.X + 1.0f, location.Y, (float)m_scene.Heightmap[(int)(location.X + 1.0f), (int)location.Y]);
            if ((location.Y + 1.0f) >= m_scene.Heightmap.Height)
                p2 = new Vector3(location.X, location.Y + 1.0f, (float)m_scene.Heightmap[(int)location.X, (int)location.Y]);
            else
                p2 = new Vector3(location.X, location.Y + 1.0f, (float)m_scene.Heightmap[(int)location.X, (int)(location.Y + 1.0f)]);
            //Find normalized vectors from p0 to p1 and p0 to p2
            Vector3 v0 = new Vector3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3 v1 = new Vector3(p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z);
            v0.Normalize();
            v1.Normalize();
            //Find the cross product of the vectors (the slope normal).
            Vector3 vsn = new Vector3();
            vsn.X = (v0.Y * v1.Z) - (v0.Z * v1.Y);
            vsn.Y = (v0.Z * v1.X) - (v0.X * v1.Z);
            vsn.Z = (v0.X * v1.Y) - (v0.Y * v1.X);
            vsn.Normalize();
            //The second part of this function does the same thing as llGround() without having to be called from a prim
            //Get the height for the integer coordinates from the Heightmap
            float baseheight = (float)m_scene.Heightmap[(int)location.X, (int)location.Y];
            //Calculate the difference between the actual coordinates and the integer coordinates
            float xdiff = location.X - (float)((int)location.X);
            float ydiff = location.Y - (float)((int)location.Y);
            //Use the equation of the tangent plane to adjust the height to account for slope
            return (((vsn.X * xdiff) + (vsn.Y * ydiff)) / (-1 * vsn.Z)) + baseheight;
        }

        void RandomizeStartMatrix()
        {
            //Generate starting matrix of random plant types
            m_cellStatus = new int[m_generations, m_xCells, m_yCells];
            m_totalSpeciesCounts = new int[m_generations, 6];
            m_coordinates = new Vector3[m_xCells, m_yCells];
            for (int y=0; y<m_yCells; y++)
            {
                for (int x=0; x<m_xCells; x++)
                {
                    float xRandomOffset = 0;
                    float yRandomOffset = 0;
                    if (m_naturalAppearance)
                    {
                        //Randomize around the matrix coordinates
                        xRandomOffset = ((float)m_random.NextDouble() - 0.5f) * m_cellSpacing;
                        yRandomOffset = ((float)m_random.NextDouble() - 0.5f) * m_cellSpacing;
                    }
                    Vector3 position = new Vector3(m_xPosition + (x * m_cellSpacing) + xRandomOffset, m_yPosition + (y * m_cellSpacing) + yRandomOffset, 0.0f);
                    //Only calculate ground level if the x,y position is within the region boundaries
                    if ((position.X >= 0) && (position.X <= 256) && (position.Y >= 0) && (position.Y <=256))
                    {
                        position.Z = GroundLevel(position);
                        //Store the coordinates so we don't have to do this again
                        m_coordinates[x, y] = position;
                        //Only assign a cellStatus if it is above water- otherwise -1 so no plant will ever be placed.
                        if (position.Z >= WaterLevel(position))
                        {
                            int newSpecies = m_random.Next(6);
                            m_cellStatus[0, x, y] = newSpecies;
                            m_totalSpeciesCounts[0, newSpecies]++;
                        }
                        else
                        {
                            m_cellStatus[0, x, y] = -1;
                        }
                    }
                    else
                    {
                        m_cellStatus[0, x, y] = -1;
                    }
                }
            }
        }

        bool ReadConfigs(string url)
        {
            //TODO: Split this function up into separate steps: Read data, parse data, generate starting matrix.
            string[] configInfo = new string[58]; //TODO: Could I import easier using xml instead of raw text?
            WebRequest configUrl = WebRequest.Create(url);
            AlertAndLog(String.Format("Reading data from url.  This may take a minute..."));
            try
            {
                StreamReader urlData = new StreamReader(configUrl.GetResponse().GetResponseStream());
                string line;
                int lineCount = 0;
                while ((line = urlData.ReadLine()) != null)
                {
                    //Chop off the <br> at the end of the line
                    //TODO: What if there is not <br>? The <br> is only there for human readability of the webapp output and if I generate the files some other way it would not be necessary
                    configInfo[lineCount] = line.Substring(0, line.LastIndexOf("<"));
                    lineCount++;
                }
                //Parse the data
                string[] matrixInfo = new string[6];
                matrixInfo = configInfo[0].Split(',');
                m_xCells = Int32.Parse(matrixInfo[0]);
                m_yCells = Int32.Parse(matrixInfo[1]);
                m_xPosition = float.Parse(matrixInfo[2]);
                m_yPosition = float.Parse(matrixInfo[3]);
                m_cellSpacing = float.Parse(matrixInfo[4]);
                if (matrixInfo[5] == "1")
                {
                    m_naturalAppearance = true;
                }
                else
                {
                    m_naturalAppearance = false;
                }
                string[] plants = new string[5];
                plants = configInfo[1].Split(',');
                for (int i = 1; i<6; i++) //Start at index 1 so index 0 stays "None"
                {
                    m_communityMembers[i] = Int32.Parse(plants[i - 1]);
                }
                for (int i=0; i<6; i++)
                {
                    string[] probabilities = new string[6];
                    probabilities = configInfo[i + 2].Split(',');
                    for(int j=0; j<6; j++)
                    {
                        m_replacementMatrix[i,j] = float.Parse(probabilities[j]);
                    }
                }
                m_cellStatus = new int[m_generations, m_xCells, m_yCells];
                m_totalSpeciesCounts = new int[m_generations, 6];
                m_coordinates = new Vector3[m_xCells, m_yCells];
                for (int y=0; y<m_yCells; y++)
                {
                    char[] startingPlants = new char[m_xCells];
                    startingPlants = configInfo[y + 8].ToCharArray();
                    for (int x=0; x<m_xCells; x++)
                    {
                        float xRandomOffset = 0;
                        float yRandomOffset = 0;
                        if (m_naturalAppearance)
                        {
                            //Randomize around the matrix coordinates
                            xRandomOffset = ((float)m_random.NextDouble() - 0.5f) * m_cellSpacing;
                            yRandomOffset = ((float)m_random.NextDouble() - 0.5f) * m_cellSpacing;
                        }
                        Vector3 position = new Vector3(m_xPosition + (x * m_cellSpacing) + xRandomOffset, m_yPosition + (y * m_cellSpacing) + yRandomOffset, 0.0f);
                        //Only calculate ground level if the x,y position is within the region boundaries
                        if ((position.X >= 0) && (position.X <= 256) && (position.Y >= 0) && (position.Y <=256))
                        {
                            position.Z = GroundLevel(position);
                            //Store the coordinates so we don't have to do this again
                            m_coordinates[x, y] = position;
                            //Only assign a cellStatus if it is above water- otherwise -1 so no plant will ever be placed.
                            if (position.Z >= WaterLevel(position))
                            {
                                if (startingPlants[x] == 'R')
                                {
                                    //Randomly select a plant type
                                    int newSpecies = m_random.Next(6);
                                    m_cellStatus[0, x, y] = newSpecies;
                                    m_totalSpeciesCounts[0, newSpecies]++;
                                }
                                else if (startingPlants[x] == 'N')
                                {
                                    //There will never be a plant here
                                    m_cellStatus[0, x, y] = -1;
                                }
                                else
                                {
                                    int newSpecies = Int32.Parse(startingPlants[x].ToString());
                                    m_cellStatus[0, x, y] = newSpecies;
                                    m_totalSpeciesCounts[0, newSpecies]++;
                                }
                            }
                            else
                            {
                                m_cellStatus[0, x, y] = -1;
                            }
                        }
                        else
                        {
                            m_cellStatus[0, x, y] = -1;
                        }
                    }
                }
                AlertAndLog(String.Format("Read from \"{0}\".  Clearing all plants and generating a new community.  This may take a minute...", url));
                return true;
            }
            catch //failed to get the data for some reason
            {
                AlertAndLog(String.Format("Error loading from \"{0}\"...", url));
                return false;
            }
        }

        void RunSimulation()
        {
            //Generate the simulation data
            int[,] age = new int[m_xCells, m_yCells];
            for (int generation=0; generation<m_generations - 1; generation++)
            {
                if (generation % 1000 == 0)
                {
                    //Provide status updates every 1000 generations
                    Alert(String.Format("Step {0} of {1}...", generation, m_generations - 1));
                }
                int nextGeneration = generation + 1;
                int rowabove;
                int rowbelow;
                int colleft;
                int colright;
                bool[,] disturbance = CalculateDisturbance();
                for (int y=0; y<m_yCells; y++)
                {
                    rowabove = y + 1;
                    rowbelow = y - 1;
                    for (int x=0; x<m_xCells; x++)
                    {
                        colright = x + 1;
                        colleft = x - 1;
                        int currentSpecies = m_cellStatus[generation, x, y];
                        if (currentSpecies != -1) //Don't ever try to update a permanent gap
                        {
                            if (disturbance[x, y])
                            {
                                m_cellStatus[nextGeneration, x, y] = 0;
                                m_totalSpeciesCounts[nextGeneration, 0]++;
                                age[x, y] = 0;
                            }
                            else
                            {
                                //Get species counts of neighbors
                                int[] neighborSpeciesCounts = GetNeighborSpeciesCounts(x, y, rowabove, rowbelow, colright, colleft, generation);
                                //Determine plant survival based on age and environment
                                bool plantSurvives = CalculateSurvival(currentSpecies, age[x, y], m_coordinates[x, y]);
                                if (plantSurvives)
                                {
                                    //Calculate replacement probabilities
                                    float[] replacementProbability = GetReplacementProbabilities(currentSpecies, neighborSpeciesCounts, generation);
                                    int newSpecies = SelectNextGenerationSpecies(replacementProbability, currentSpecies);
                                    if (newSpecies == -1)
                                    {
                                        //The old plant is still there
                                        age[x, y]++;
                                        m_cellStatus[nextGeneration, x, y] = currentSpecies;
                                        m_totalSpeciesCounts[nextGeneration, currentSpecies]++;
                                    }
                                    else
                                    {
                                        //The old plant has been replaced (though possibly by another of the same species...)
                                        age[x, y] = 0;
                                        m_cellStatus[nextGeneration, x, y] = newSpecies;
                                        m_totalSpeciesCounts[nextGeneration, newSpecies]++;
                                    }
                                }
                                else
                                {
                                    //Calculate replacement probabilities based on a gap
                                    float[] replacementProbability = GetReplacementProbabilities(0, neighborSpeciesCounts, generation);
                                    age[x, y] = 0;
                                    int newSpecies = SelectNextGenerationSpecies(replacementProbability, 0);
                                    if (newSpecies == -1)
                                    {
                                        //No new plant was selected.  It will be a gap.
                                        m_cellStatus[nextGeneration, x, y] = 0;
                                        m_totalSpeciesCounts[nextGeneration, 0]++;
                                    }
                                    else
                                    {
                                        m_cellStatus[nextGeneration, x, y] = newSpecies;
                                        m_totalSpeciesCounts[nextGeneration, newSpecies]++;
                                    }
                                }
                            }
                        }
                        else
                        {
                            m_cellStatus[nextGeneration, x, y] = -1;
                        }
                    }
                }
            }
        }

        bool CalculateSurvival(int species, int age, Vector3 coordinates)
        {
            //Return true if the plant survives or false if it does not

            //Generate a float from 0-1.0 representing the probability of survival
            float ageHealth = CalculateAgeHealth(age, m_ageMaximum[species]);
            float altitudeHealth = CalculateAltitudeHealth(coordinates.Z, m_altitudeOptimal[species], m_altitudeShape[species]);
            Vector3 soilType = GetSoilType(coordinates);
            float salinityHealth = CalculateSoilHealth(soilType.X, m_salinityOptimal[species], m_salinityShape[species]);
            float drainageHealth = CalculateSoilHealth(soilType.Y, m_drainageOptimal[species], m_drainageShape[species]);
            float fertilityHealth = CalculateSoilHealth(soilType.Z, m_fertilityOptimal[species], m_fertilityShape[species]);
            float survivalProbability = ageHealth + altitudeHealth + salinityHealth + drainageHealth + fertilityHealth;
            //Select a random float from 0-1.0.  Plant survives if random number <= probability of survival
            float randomFloat = (float)m_random.NextDouble();
            if (randomFloat <= survivalProbability)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        float CalculateSoilHealth(float actual, float optimal, float shape)
        {
            //Returns a value from 0-1.0 representing the health of an individual with an 'actual' value for some environmental parameter given the optimal value and shape. This function works for things like soil values where the actual values will range from 0-1.0.
            float health = 1.0f - (Math.Abs(optimal - actual) * shape);
            if (health > 1.0f)
            {
                health = 1.0f;
            }
            if (health < 0f)
            {
                health = 0f;
            }
            return health;
        }

        float CalculateAltitudeHealth(float actual, float optimal, float shape)
        {
            //Returns a value from 0-1.0 representing the health of an individual with an 'actual' value for some environmental parameter given the optimal value and shape. This function works for altitude.
            float health = 1.0f - (Math.Abs(optimal - actual / 50f) * shape);
            if (health > 1.0f)
            {
                health = 1.0f;
            }
            if (health < 0f)
            {
                health = 0f;
            }
            return health;
        }

        float CalculateAgeHealth(int actual, int maximum)
        {
            //Returns a value from 0-1.0 representing the health of an individual with an 'actual' value for some environmental parameter given the optimal value and shape. This function works for age or others parameters with a miximum rather than optimal value.
            float health = ((maximum - actual) / (float)maximum);
            if (health > 1.0f)
            {
                health = 1.0f;
            }
            if (health < 0f)
            {
                health = 0f;
            }
            return health;
        }

        bool[,] CalculateDisturbance()
        {
            //TODO: This needs to generate a disturbance matrix based on settings from the webform.
            //For now it just returns a matrix with a 100:1 false:true ratio
            bool[,] disturbanceMatrix = new bool[m_xCells, m_yCells];
            for (int y=0; y<m_yCells; y++)
            {
                for (int x=0; x<m_xCells; x++)
                {
                    if (m_random.Next(100) == 0)
                    {
                        disturbanceMatrix[x, y] = true;
                    }
                    else
                    {
                        disturbanceMatrix[x, y] = false;
                    }
                }
            }
            return disturbanceMatrix;
        }

        int SelectNextGenerationSpecies(float[] replacementProbability, int currentSpecies)
        {
            //Randomly determine the new species based on the replacement probablilities
            float randomReplacement = (float)m_random.NextDouble();
            if (randomReplacement <= replacementProbability[0])
            {
                return 0;
            }
            else if (randomReplacement <= replacementProbability[1] + replacementProbability[0])
            {
                return 1;
            }
            else if (randomReplacement <= replacementProbability[2] + replacementProbability[1] + replacementProbability[0])
            {
                return 2;
            }
            else if (randomReplacement <= replacementProbability[3] + replacementProbability[2] + replacementProbability[1] + replacementProbability[0])
            {
                return 3;
            }
            else if (randomReplacement <= replacementProbability[4] + replacementProbability[3] + replacementProbability[2] + replacementProbability[1] + replacementProbability[0])
            {
                return 4;
            }
            else if (randomReplacement <= replacementProbability[5] + replacementProbability[4] + replacementProbability[3] + replacementProbability[2] + replacementProbability[1] + replacementProbability[0])
            {
                return 5;
            }
            else
            {
                return currentSpecies;
            }
        }

        public float WaterLevel(Vector3 location)
        {
            //Return the water level at the specified location.
            //This function performs essentially the same function as llWater() without having to be called by a prim.
            return (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
        }

        public Vector3 GetSoilType(Vector3 location)
        {
            //Return the soiltype vector (salinity, drainage, fertility) for a location.
            //This function performs essentially the same function as osSoilType() without having to be called by a prim.
            IvpgSoilModule module = m_scene.RequestModuleInterface<IvpgSoilModule>();
            Vector3 soil = new Vector3(0f,0f,0f);
            if (module != null)
            {
                int x = (int)location.X;
                int y = (int)location.Y;
                soil = module.SoilType(x, y, 0);
            }
            return soil;
        }

        #endregion
    }
}
