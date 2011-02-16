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
using System.Xml;
using System.Collections.Generic;

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

        //int[] m_communityMembers = new int[6] {0, 1, 2, 5, 16, 18}; //Default plants to include in the community
        int[] m_communityMembers = new int[6] {0, 16, 17, 18, 19, 20}; //DEBUG- smaller plants
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
                                                       {1f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f},
                                                       {1f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f},
                                                       {1f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f},
                                                       {1f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f},
                                                       {1f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f}};

        //TODO: These need to come from the webtool!
        int[] m_lifespans = new int[6] {0, 20, 20, 20, 20, 20}; //Maximum age for each species
        //Optimal values and shape parameters for each species
        float[] m_altitudeOptimums = new float[6] {0f, 20f, 20f, 20f, 20f, 20f};
        float[] m_altitudeEffects = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_salinityOptimums = new float[6] {0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f};
        float[] m_salinityEffects = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_drainageOptimums = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_drainageEffects = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_fertilityOptimums = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        float[] m_fertilityEffects = new float[6] {0f, 0f, 0f, 0f, 0f, 0f};
        bool m_disturbanceOnly = false;
        float m_ongoingDisturbanceRate = 0.0f;
        string m_simulationId = "defaults";
        //TODO: These map values are being read from the webform but we aren't using them for anything
        int m_terrainMap = 0;
        int m_salinityMap = 0;
        int m_drainageMap = 0;
        int m_fertilityMap = 0;

        int m_currentGeneration = 0; //The currently displayed generation
        bool m_isReverse = false; //Whether we are stepping backward through the simulation
        int[,] m_displayedPlants; //Tracks the currently displayed plants
        int[,] m_totalSpeciesCounts; //Total species counts for each generation.
        int m_totalActiveCells; //The total number of possible plant locations (plants + gaps + disturbed areas ... or ... xCells * yCells - cells-below-water). This shouldn't change over the course of a simulation.
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
                CalculateStatistics(m_currentGeneration, m_currentGeneration, false);
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

        public void Log(string message)
        {
            m_log.DebugFormat("[{0}] {1}", Name, message);
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

        void CalculateStatistics(int generation, int lastVisualizedGeneration, bool needToLog)
        {
            //Generates data to send to the log and the hud.
            //TODO: The logging should probably happen during the simulation, not during the visualization?
            string[] hudString = new string[5];
            hudString[0] = String.Format("Generation: {0}", generation);
            hudString[1] = "Species";
            hudString[2] = "Qty";
            hudString[3] = "Change";
            hudString[4] = "%";
            string logString = generation.ToString();
            int totalPlants = m_totalActiveCells - m_totalSpeciesCounts[generation, 0];
            for (int i=1; i<6; i++)
            {
                hudString[1] += "\n" + i;
                hudString[2] += "\n" + m_totalSpeciesCounts[generation, i];
                int qtyChange = m_totalSpeciesCounts[generation, i] - m_totalSpeciesCounts[lastVisualizedGeneration, i];
                string direction = "";
                if (qtyChange > 0)
                {
                    direction = "+";
                }
                hudString[3] += "\n" + direction + qtyChange;
                float percent;
                if (totalPlants > 0) //Avoid divide-by-zero errors
                {
                    percent = (float)(Math.Round((double)((m_totalSpeciesCounts[generation, i] / (float)totalPlants) * 100), 1));
                }
                else
                {
                    percent = 0f;
                }
                hudString[4] += "\n" + percent + "%";
                logString += String.Format(",{0}", m_totalSpeciesCounts[generation, i]);
            }
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
            CalculateStatistics(nextGeneration, m_currentGeneration, true);
            m_currentGeneration = nextGeneration;
        }

        #endregion

        #region Simulation-specific functions

        int[] GetNeighborSpeciesCounts(int x, int y, int rowabove, int rowbelow, int colright, int colleft, int generation)
        {
            //Get counts of neighborspecies
            //Edge cells will have fewer neighbors.  That is ok.  We only care about the count of neighbors of each species so a neighbor that is a gap or off the edge of the matrix doesn't matter.
            int[] neighborSpeciesCounts = new int[6] {0, 0, 0, 0, 0, 0};
            int neighborType;
            if (colleft >= 0)
            {
                neighborType = m_cellStatus[generation, colleft, y];
                if (neighborType != -1) //Don't count permanent gaps
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
            //Log(neighborSpeciesCounts[0].ToString() + " " + neighborSpeciesCounts[1].ToString() + " " + neighborSpeciesCounts[2].ToString() + " " + neighborSpeciesCounts[3].ToString() + " " + neighborSpeciesCounts[4].ToString() + " " + neighborSpeciesCounts[5].ToString()); //DEBUG
            return neighborSpeciesCounts;
        }

        float[] GetReplacementProbabilities(int currentSpecies, int[] neighborSpeciesCounts, int generation)
        {
            //Calculate the probability that the current plant will be replaced by each species.
            //The first value is always 0 because gaps cannot replace a plant through competition. Gaps arise only when a plant dies and no replacement is selected.
            float[] replacementProbabilities = new float[6];
            //Log("Current: " + currentSpecies.ToString()); //DEBUG
            //Log("Total: " + m_totalActiveCells.ToString()); //DEBUG
            for (int species=1; species<6; species++)
            {
                replacementProbabilities[species] = ((m_replacementMatrix[species, currentSpecies] * ((float)neighborSpeciesCounts[species] / 8.0f)) * 0.80f) + ((m_replacementMatrix[species, currentSpecies] * ((float)m_totalSpeciesCounts[generation, species] / m_totalActiveCells)) * 0.1995f) + 0.0005f; //80% local, 19.95% distant, 0.05% out-of-area
                //Log(species.ToString() + " " + neighborSpeciesCounts[species].ToString() + " " + m_totalSpeciesCounts[generation, species].ToString() + " " + replacementProbabilities[species].ToString()); //DEBUG
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
            //Generate starting matrix of random plant types and determine the region x,y,z coordinates where each plant will be placed
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
                        //Randomize around the matrix coordinates for a more natural (less crop-like) appearance
                        xRandomOffset = ((float)m_random.NextDouble() - 0.5f) * m_cellSpacing;
                        yRandomOffset = ((float)m_random.NextDouble() - 0.5f) * m_cellSpacing;
                    }
                    Vector3 position = new Vector3(m_xPosition + (x * m_cellSpacing) + xRandomOffset, m_yPosition + (y * m_cellSpacing) + yRandomOffset, 0.0f);
                    if ((position.X >= 0) && (position.X <= 256) && (position.Y >= 0) && (position.Y <=256))
                    {
                        //Only calculate ground level if the x,y position is within the region boundaries
                        position.Z = GroundLevel(position);
                        //Store the coordinates so we don't have to do this again
                        m_coordinates[x, y] = position;
                        if (position.Z >= WaterLevel(position))
                        {
                            //Assign a random plant type if the coordinates are above water
                            int newSpecies = m_random.Next(6);
                            m_cellStatus[0, x, y] = newSpecies;
                            m_totalSpeciesCounts[0, newSpecies]++;
                        }
                        else
                        {
                            //If the coordinates are below water make it a permanent gap
                            m_cellStatus[0, x, y] = -1;
                        }
                    }
                    else
                    {
                        //If the x,y position is outside the region boundaries make it a permanent gap
                        m_cellStatus[0, x, y] = -1;
                    }
                }
            }
             m_totalActiveCells = m_totalSpeciesCounts[0, 0] + m_totalSpeciesCounts[0, 1] + m_totalSpeciesCounts[0, 2] + m_totalSpeciesCounts[0, 3] + m_totalSpeciesCounts[0, 4] + m_totalSpeciesCounts[0, 5]; //This total number of active cells will remain constant unless we load new parameters from the webform.
        }

        bool ReadConfigs(string url)
        {
            //Conversion tables for the "None", "Low", "Mid", "High" values on the webform
            //TODO: Should these be global?
            //Log("Entered ReadConfigs()"); //DEBUG
            Dictionary<string, float> convertReplacement = new Dictionary<string, float>(){
                {"N", 0.0f}, {"L", 0.1f}, {"M", 0.25f}, {"H", 0.5f}};
            Dictionary<string, int> convertLifespans = new Dictionary<string, int>(){
                {"S", 1}, {"M", 10}, {"L", 25}};
            Dictionary<string, float> convertAltitudeOptimums = new Dictionary<string, float>(){
                {"L", 20.0f}, {"M", 35.0f}, {"H", 50.0f}};
            Dictionary<string, float> convertAltitudeEffects = new Dictionary<string, float>(){
                {"N", 0.0f}, {"L", 0.5f}, {"M", 1.0f}, {"H", 2.0f}};
            Dictionary<string, float> convertSoilOptimums = new Dictionary<string, float>(){
                {"L", 0.0f}, {"M", 0.5f}, {"H", 1.0f}};
            Dictionary<string, float> convertSoilEffects = new Dictionary<string, float>(){
                {"N", 0.0f}, {"L", 0.1f}, {"M", 0.5f}, {"H", 1.0f}};
            Dictionary<string, float> convertOngoingDisturbance = new Dictionary<string, float>(){
                {"N", 0.0f}, {"L", 0.0001f}, {"M", 0.01f}, {"H", 0.1f}};
            //Read configuration data from a url.  This works with the vMeadowGA google app version2 (the xml version).
            WebRequest configUrl = WebRequest.Create(url);
            //Log("Sent WebRequest"); //DEBUG
            AlertAndLog(String.Format("Reading data from url.  This may take a minute..."));
            try
            {
                //Read the xml data from the webform into a dictionary of parameters
                Dictionary<string, string> newParameters = new Dictionary<string, string>();
                XmlTextReader reader = new XmlTextReader(url);
                reader.WhitespaceHandling = WhitespaceHandling.Significant;
                while (reader.Read())
                {
                    if (reader.Name == "property")
                    {

                        string parameterName = reader.GetAttribute("name");
                        string parameterValue = reader.ReadString();
                        newParameters.Add(parameterName, parameterValue);
                    }
                }
                //Log("Read XML to Dictionary"); //DEBUG
                //foreach(var pair in newParameters) //DEBUG
                //{ //DEBUG
                //    Log(pair.Key + " " + pair.Value); //DEBUG
                //}  //DEBUG
                if (newParameters["disturbance_only"] == "1")
                    {
                        m_disturbanceOnly = true;
                    }
                    else
                    {
                        m_disturbanceOnly = false;
                    }
                if (m_disturbanceOnly == false)
                {
                    //Log("Not DisturbanceOnly"); //DEBUG
                    //Store all parameters
                    //Matrix parameters
                    m_simulationId = newParameters["id"]; //TODO: Display this on vpcHUD
                    m_xCells = Int32.Parse(newParameters["x_size"]);
                    m_yCells = Int32.Parse(newParameters["y_size"]);
                    m_xPosition = float.Parse(newParameters["x_location"]);
                    m_yPosition = float.Parse(newParameters["y_location"]);
                    m_cellSpacing = float.Parse(newParameters["spacing"]);
                    if (newParameters["natural"] == "1")
                    {
                        m_naturalAppearance = true;
                    }
                    else
                    {
                        m_naturalAppearance = false;
                    }
                    m_terrainMap = Int32.Parse(newParameters["terrain"]);
                    m_salinityMap = Int32.Parse(newParameters["salinity"]);
                    m_drainageMap = Int32.Parse(newParameters["drainage"]);
                    m_fertilityMap = Int32.Parse(newParameters["fertility"]);
                    //Log("Stored matrix parameters"); //DEBUG
                    //Plant characteristics parameters
                    string[] communityMembers = newParameters["plant_types"].Split(',');
                    string[] lifespans = newParameters["lifespans"].Split(',');
                    string[] altitudeOptimums = newParameters["altitude_optimums"].Split(',');
                    string[] altitudeEffects = newParameters["altitude_effects"].Split(',');
                    string[] salinityOptimums = newParameters["salinity_optimums"].Split(',');
                    string[] salinityEffects = newParameters["salinity_effects"].Split(',');
                    string[] drainageOptimums = newParameters["drainage_optimums"].Split(',');
                    string[] drainageEffects = newParameters["drainage_effects"].Split(',');
                    string[] fertilityOptimums = newParameters["fertility_optimums"].Split(',');
                    string[] fertilityEffects = newParameters["fertility_effects"].Split(',');
                    for (int i = 1; i<6; i++) //Start at index 1 so index 0 stays "None" to represent gaps
                    {
                        m_communityMembers[i] = Int32.Parse(communityMembers[i - 1]);
                        m_lifespans[i] = convertLifespans[lifespans[i-1]];
                        m_altitudeOptimums[i] = convertAltitudeOptimums[altitudeOptimums[i-1]];
                        m_altitudeEffects[i] = convertAltitudeEffects[altitudeEffects[i-1]];
                        m_salinityOptimums[i] = convertSoilOptimums[salinityOptimums[i-1]];
                        m_salinityEffects[i] = convertSoilEffects[salinityEffects[i-1]];
                        m_drainageOptimums[i] = convertSoilOptimums[drainageOptimums[i-1]];
                        m_drainageEffects[i] = convertSoilEffects[drainageEffects[i-1]];
                        m_fertilityOptimums[i] = convertSoilOptimums[fertilityOptimums[i-1]];
                        m_fertilityEffects[i] = convertSoilEffects[fertilityEffects[i-1]];
                    }
                    //Log("Stored plant characteristics"); //DEBUG
                    //Replacement probability parameters
                    for (int i=1; i<6; i++) //Start at index 1 so the gap replacement probabilities stay 0 (since gaps have no replacement probability)
                    {
                        string[] probabilities = newParameters["replacement_" + i.ToString()].Split(',');
                        for(int j=0; j<6; j++)
                        {
                            m_replacementMatrix[i,j] = convertReplacement[probabilities[j]];
                        }
                    }
                    //Log("Stored replacement matrix"); //DEBUG
                    //Disturbance parameters and starting matrix
                    m_ongoingDisturbanceRate = convertOngoingDisturbance[newParameters["ongoing_disturbance"]];
                    char[] startingPlants = new char[m_xCells];
                    startingPlants = newParameters["starting_matrix"].ToCharArray();
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
                            //TODO: We need to have loaded any new terrain file before doing this
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
                                    int currentCell = (y * m_xCells) + x;
                                    if (startingPlants[currentCell] == 'R')
                                    {
                                        //Randomly select a plant type
                                        int newSpecies = m_random.Next(6);
                                        m_cellStatus[0, x, y] = newSpecies;
                                        m_totalSpeciesCounts[0, newSpecies]++;
                                    }
                                    else if (startingPlants[currentCell] == 'N')
                                    {
                                        //TODO: This needs to be handled differently.  There should be a permanentDisturbanceMap to store these and their cell status should be set to 0, not -1.  -1 should be just for underwater plants.  That way I can display the count of 0's as the gaps (both manmade and natural). The CalculateDisturbance function should use the permanentDisturbanceMap as a starting point and randomly generate temporary disturbance based on the ongoing disturbance rate.
                                        //There will never be a plant here
                                        m_cellStatus[0, x, y] = -1;
                                    }
                                    else
                                    {
                                        int newSpecies = Int32.Parse(startingPlants[currentCell].ToString());
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
                    m_totalActiveCells = m_totalSpeciesCounts[0, 0] + m_totalSpeciesCounts[0, 1] + m_totalSpeciesCounts[0, 2] + m_totalSpeciesCounts[0, 3] + m_totalSpeciesCounts[0, 4] + m_totalSpeciesCounts[0, 5]; //This count only changes when we load new parameters from the webform.
                    AlertAndLog(String.Format("Read from \"{0}\".  Clearing all plants and generating a new community.  This may take a minute...", url));
                }
                else
                {
                    //Log("Disturbance Only"); //DEBUG
                    //Store just the 'disturbance-related parameters
                    m_ongoingDisturbanceRate = convertOngoingDisturbance[newParameters["ongoing_disturbance"]];
                    char[] startingPlants = new char[m_xCells];
                    startingPlants = newParameters["starting_matrix"].ToCharArray();
                    for (int y=0; y<m_yCells; y++)
                    {
                        for (int x=0; x<m_xCells; x++)
                        {
                            if (m_coordinates[x, y].Z >= WaterLevel(m_coordinates[x, y]))
                            {
                                //We only care about disturbances above water
                                int currentCell = (y * m_xCells) + x;
                                if (startingPlants[currentCell] == 'N')
                                {
                                    //TODO: This needs to be handled differently.  There should be a permanentDisturbanceMap to store these and their cell status should be set to 0, not -1.  -1 should be just for underwater plants.  That way I can display the count of 0's as the gaps (both manmade and natural). The CalculateDisturbance function should use the permanentDisturbanceMap as a starting point and randomly generate temporary disturbance based on the ongoing disturbance rate.
                                    //There will never be a plant here
                                    int oldCellStatus = m_cellStatus[0, x, y];
                                    if (oldCellStatus != -1)
                                    {
                                        //Update the total species counts
                                        m_totalSpeciesCounts[0, oldCellStatus]--;
                                        m_cellStatus[0, x, y] = -1;
                                    }
                                }
                            }
                        }
                    }
                }
                return true;
            }
            catch
            {
                //Failed to get the data for some reason
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
                //TODO: Do we really need to get the disturbance values every generation?  It depends on if we are using only permanent disturbance defined on the webform or if we also want to have some underlying level of ongoing new disturbance (perhaps with the level set via the webform too).  If we don't have ongoing new disturbance we could move the disturbance array declaration outside of the generation loop so the function only gets called once (or better yet, make it global and set its values when we start the region or import new parameters from the webform.
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
                                    //Calculate replacement probabilities based on current plant
                                    float[] replacementProbability = GetReplacementProbabilities(currentSpecies, neighborSpeciesCounts, generation);
                                    //Determine the next generation plant based on those probabilities
                                    int newSpecies = SelectNextGenerationSpecies(replacementProbability, currentSpecies);
                                    if (newSpecies == -1)
                                    {
                                        //Log("Still there"); //DEBUG
                                        //The old plant is still there
                                        age[x, y]++;
                                        m_cellStatus[nextGeneration, x, y] = currentSpecies;
                                        m_totalSpeciesCounts[nextGeneration, currentSpecies]++;
                                    }
                                    else
                                    {
                                        //Log("Replaced"); //DEBUG
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
                                    //Determine the next generation plant based on those probabilities
                                    int newSpecies = SelectNextGenerationSpecies(replacementProbability, 0);
                                    if (newSpecies == -1)
                                    {
                                        //No new plant was selected.  It will still be a gap.
                                        m_cellStatus[nextGeneration, x, y] = 0;
                                        m_totalSpeciesCounts[nextGeneration, 0]++;
                                    }
                                    else
                                    {
                                        //Store the new plant status and update the total species counts
                                        m_cellStatus[nextGeneration, x, y] = newSpecies;
                                        m_totalSpeciesCounts[nextGeneration, newSpecies]++;
                                    }
                                }
                            }
                        }
                        else
                        {
                            //Permanent gaps stay gaps
                            m_cellStatus[nextGeneration, x, y] = -1;
                        }
                    }
                }
            }
        }

        bool CalculateSurvival(int species, int age, Vector3 coordinates)
        {
            //Return true if the plant survives or false if it does not
            if (species == 0) //If there is no plant it can't possibly survive...
            {
                return false;
            }
            else
            {
                //Generate a float from 0-1.0 representing the probability of survival based on plant age, and altitude
                float ageHealth = CalculateAgeHealth(age, m_lifespans[species]);
                float altitudeHealth = CalculateAltitudeHealth(coordinates.Z, m_altitudeOptimums[species], m_altitudeEffects[species]);
                //Get the soil values for the plant's coordinates and calculate a probability of survival based on those values
                Vector3 soilType = GetSoilType(coordinates);
                float salinityHealth = CalculateSoilHealth(soilType.X, m_salinityOptimums[species], m_salinityEffects[species]);
                float drainageHealth = CalculateSoilHealth(soilType.Y, m_drainageOptimums[species], m_drainageEffects[species]);
                float fertilityHealth = CalculateSoilHealth(soilType.Z, m_fertilityOptimums[species], m_fertilityEffects[species]);
                //Overall survival probability is the product of these separate survival probabilities
                float survivalProbability = ageHealth * altitudeHealth * salinityHealth * drainageHealth * fertilityHealth;
                //Select a random float from 0-1.0.  Plant survives if random number <= probability of survival
                float randomFloat = (float)m_random.NextDouble();
                if (randomFloat <= survivalProbability)
                {
                    //Plant survives
                    //Log("Health: " + survivalProbability.ToString() + " Random: " + randomFloat.ToString() + " Survived"); //DEBUG
                    return true;
                }
                else
                {
                    //Plant does not survive
                    //Log("Health: " + survivalProbability.ToString() + " Random: " + randomFloat.ToString() + " Died"); //DEBUG
                    return false;
                }
            }
        }

        float CalculateSoilHealth(float actual, float optimal, float shape)
        {
            //Returns a value from 0-1.0 representing the health of an individual with an 'actual' value for some environmental parameter given the optimal value and shape. This function works for things like soil values where the actual values will range from 0-1.0.  It doesn't have to be soil values. With an optimal of 1.0 and a shape of 1, values range (linearly) from 1.0 at 1.0 to 0.0 at 0.0.  Lower values for shape flatten the 'fitness curve'. With shape <= 0, health will always equal 1.
            float health = 1.0f - (Math.Abs(optimal - actual) * shape);
            //Log("Soil: " + health.ToString() + " " + actual.ToString() + " " + optimal.ToString() + " " + shape.ToString()); //DEBUG
            //Don't allow return values >1 or <0
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
            //Returns a value from 0-1.0 representing the health of an individual with an 'actual' value for some environmental parameter given the optimal value and shape. This function works for altitude.  With an optimal of 50 and a shape of 1, values range (linearly) from 1.0 at 50m to 0.0 at 0m.  Lower values for shape flatten the 'fitness curve'. With shape <= 0, health will always equal 1.0.
            float health = 1.0f - (Math.Abs(((optimal - actual) / 50f)) * shape);
            //Log("Altitude: " + health.ToString() + " " + actual.ToString() + " " + optimal.ToString() + " " + shape.ToString()); //DEBUG
            //Don't allow return values >1 or <0
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

        float CalculateAgeHealth(int actual, int maximumAge)
        {
            //Returns a value from 0-1.0 representing the health of an individual with an 'actual' value for some environmental parameter given the optimal value and shape. This function works for age or others parameters with a maximum rather than optimal value. Health is highest (1.0) when age = 0 and decreases linearly to 0.0 when age = maximumAge.
            float health = ((maximumAge - actual) / (float)maximumAge);
            //Log("Age: " + health.ToString() + " " + actual.ToString() + " " + maximum.ToString()); //DEBUG
            //Don't allow return values >1 or <0
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
            //Returns a matrix of true and false values representing 'disturbed' areas where no plants will be allowed to grow, and 'undisturbed' locations where plants will grow normally.
            //TODO: This needs to generate a disturbance matrix based on settings from the webform. Atul is working on the user interface for the webform. For now, this function just returns a random matrix with a 100:1 false:true ratio.
            bool[,] disturbanceMatrix = new bool[m_xCells, m_yCells];
            for (int y=0; y<m_yCells; y++)
            {
                for (int x=0; x<m_xCells; x++)
                {
                    if (m_random.Next(100) == 0)
                    {
                        disturbanceMatrix[x, y] = true;
                        //Log("Disturbance at (" + x.ToString() + ", " + y.ToString() + ")"); //DEBUG
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
            //Randomly determine the new species based on the replacement probablilities.  We aren't concerned with the probability of replacement by no plant, since we are looking at competition between species here.
            float randomReplacement = (float)m_random.NextDouble();
            if (randomReplacement <= replacementProbability[1])
            {
                //Log(currentSpecies.ToString() + " 1    " +randomReplacement.ToString() + " " + replacementProbability[0].ToString() + " " + replacementProbability[1].ToString() + " " + replacementProbability[2].ToString() + " " + replacementProbability[3].ToString() + " " + replacementProbability[4].ToString() + " " + replacementProbability[5].ToString()); //DEBUG
                return 1;
            }
            else if (randomReplacement <= replacementProbability[2] + replacementProbability[1])
            {
                //Log(currentSpecies.ToString() + " 2    " +randomReplacement.ToString() + " " + replacementProbability[0].ToString() + " " + replacementProbability[1].ToString() + " " + replacementProbability[2].ToString() + " " + replacementProbability[3].ToString() + " " + replacementProbability[4].ToString() + " " + replacementProbability[5].ToString());  //DEBUG
                return 2;
            }
            else if (randomReplacement <= replacementProbability[3] + replacementProbability[2] + replacementProbability[1])
            {
                //Log(currentSpecies.ToString() + " 3    " +randomReplacement.ToString() + " " + replacementProbability[0].ToString() + " " + replacementProbability[1].ToString() + " " + replacementProbability[2].ToString() + " " + replacementProbability[3].ToString() + " " + replacementProbability[4].ToString() + " " + replacementProbability[5].ToString());  //DEBUG
                return 3;
            }
            else if (randomReplacement <= replacementProbability[4] + replacementProbability[3] + replacementProbability[2] + replacementProbability[1])
            {
                //Log(currentSpecies.ToString() + " 4    " +randomReplacement.ToString() + " " + replacementProbability[0].ToString() + " " + replacementProbability[1].ToString() + " " + replacementProbability[2].ToString() + " " + replacementProbability[3].ToString() + " " + replacementProbability[4].ToString() + " " + replacementProbability[5].ToString());  //DEBUG
                return 4;
            }
            else if (randomReplacement <= replacementProbability[5] + replacementProbability[4] + replacementProbability[3] + replacementProbability[2] + replacementProbability[1])
            {
                //Log(currentSpecies.ToString() + " 5    " +randomReplacement.ToString() + " " + replacementProbability[0].ToString() + " " + replacementProbability[1].ToString() + " " + replacementProbability[2].ToString() + " " + replacementProbability[3].ToString() + " " + replacementProbability[4].ToString() + " " + replacementProbability[5].ToString());  //DEBUG
                return 5;
            }
            else
            {
                //Indicate that the current plant was not replaced (we use -1 for this because returning the current species integer would indicate that the current individual was replaced by a new member of the same species.
                //Log(currentSpecies.ToString() + " -    " +randomReplacement.ToString() + " " + replacementProbability[0].ToString() + " " + replacementProbability[1].ToString() + " " + replacementProbability[2].ToString() + " " + replacementProbability[3].ToString() + " " + replacementProbability[4].ToString() + " " + replacementProbability[5].ToString()); //DEBUG
                return -1;
            }
        }

        public float WaterLevel(Vector3 location)
        {
            //Return the water level at the specified location.
            //This function performs the same function as llWater() without having to be called by a prim.
            return (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
        }

        public Vector3 GetSoilType(Vector3 location)
        {
            //Return the soiltype vector (salinity, drainage, fertility) for a location.
            //This function performs the same function as osSoilType() without having to be called by a prim.
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
