/*
* Copyright (c) Contributors http://github.com/aduffy70/CellularAutomata
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
* * Redistributions of source code must retain the above copyright
* notice, this list of conditions and the following disclaimer.
* * Redistributions in binary form must reproduce the above copyright
* notice, this list of conditions and the following disclaimer in the
* documentation and/or other materials provided with the distribution.
* * Neither the name of the CellularAutomata module nor the
* names of its contributors may be used to endorse or promote products
* derived from this software without specific prior written permission.
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
using System.Collections.Generic;
using System.Reflection;
using System.Timers;

using log4net;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace CellularAutomataModule
    {
    public class CellularAutomataModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		bool m_enabled = false;
        List<SceneObjectGroup> m_prims = new List<SceneObjectGroup>();
		float m_xPos = 128f; //inworld x coordinate for the 0,0 position
		float m_yPos = 128f;  //inworld y coordinate for the 0,0 position
		float m_zPos = 25f;  //inworld z height for the grid
		int m_xCells = 16;
		int m_yCells = 16;
        float m_cellSize = 0.9f; //X and Y size of the cells (they are always z=0.1)
        float m_cellSpacing = 0.1f; //Gap between cells
		float m_offset = 0.25f; //The automata offset to keep the cells from all converging on the mean
		float[] m_matrix;
        private Scene m_scene;
        Timer m_timer = new Timer(); //Timer to replace the region heartbeat
        Random m_random = new Random();
        int m_cycleTime = 10000;

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            IConfig cellularAutomataConfig = config.Configs["CellularAutomata"];
            if (cellularAutomataConfig != null)
            {
                m_enabled = cellularAutomataConfig.GetBoolean("enabled", true);
                m_xPos = cellularAutomataConfig.GetFloat("x_position", 128.0f);
                m_yPos = cellularAutomataConfig.GetFloat("y_position", 128.0f);
                m_zPos = cellularAutomataConfig.GetFloat("z_position", 25.0f);
                m_xCells = cellularAutomataConfig.GetInt("x_cells", 16);
                m_yCells = cellularAutomataConfig.GetInt("y_cells", 16);
                m_offset = cellularAutomataConfig.GetFloat("offset", 0.25f);
                m_cycleTime = cellularAutomataConfig.GetInt("cycle_time", 10) * 1000;
                m_cellSize = cellularAutomataConfig.GetFloat("cell_size", 0.9f);
                m_cellSpacing = cellularAutomataConfig.GetFloat("cell_spacing", 0.1f);
            }
            if (m_enabled)
            {
                m_matrix = new float[m_xCells * m_yCells];
                m_scene = scene;
            }
        }

        public void PostInitialise()
        {
            if (m_enabled)
            {
                m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
                m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
                InitializeMatrix(m_scene);
                //Start the timer
                m_timer.Elapsed += new ElapsedEventHandler(OnTimer);
                m_timer.Interval = m_cycleTime;
                m_timer.Start();

            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get
            {
                return "CellularAutomataModule";
            }
        }

        public bool IsSharedModule
        {
            get
            {
                return false;
            }
        }

        #endregion

        void InitializeMatrix(Scene scene)
        {
            // We're going to place a grid of objects in world
			for (int y=0; y<m_yCells; y++)
            {
				for (int x=0; x<m_xCells; x++)
                {
					Vector3 pos = new Vector3(m_xPos + (x * (m_cellSize + m_cellSpacing)), m_yPos + (y * (m_cellSize + m_cellSpacing)), m_zPos);
					PrimitiveBaseShape prim = PrimitiveBaseShape.CreateBox();
					prim.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a221fe21f"));
					SceneObjectGroup sog = new SceneObjectGroup(UUID.Zero, pos, prim);
					sog.RootPart.Scale = new Vector3(m_cellSize, m_cellSize, 0.1f);
					Primitive.TextureEntry tex = sog.RootPart.Shape.Textures;
					float randomValue = (float)m_random.NextDouble();
					Color4 texcolor = new Color4(randomValue, randomValue, randomValue, 1.0f);
					m_matrix[y * m_xCells + x] = randomValue;
					tex.DefaultTexture.RGBA = texcolor;
					sog.RootPart.UpdateTexture(tex);
					m_prims.Add(sog);
				}
			}
			//Add these objects to the list of managed objects
			//Place the objects visibly on the scene
			foreach (SceneObjectGroup sogr in m_prims)
            {
				scene.AddNewSceneObject(sogr, false);
            }
		}

        void OnChat(Object sender, OSChatMessage chat)
        {
            if ((chat.Channel != 4) || (chat.Message.Length < 5))
            {
				return;
            }
			else
            {
				if (chat.Message.Substring(0,5) == "reset")
                {
					if (chat.Message.Length > 6)
                    {
						m_offset = float.Parse(chat.Message.Substring(6));
					}
                    RandomizeMatrix();
                }
            }
        }

        void RandomizeMatrix()
        {
			for (int y=0; y<m_yCells; y++)
            {
				for (int x=0; x<m_xCells; x++)
                {
                    int index = y * m_xCells + x;
				    float randomValue = (float)m_random.NextDouble();
					Color4 texcolor = new Color4(randomValue, randomValue, randomValue, 1.0f);
					m_matrix[index] = randomValue;
					Primitive.TextureEntry tex = m_prims[index].RootPart.Shape.Textures;
					tex.DefaultTexture.RGBA = texcolor;
                    m_prims[index].RootPart.UpdateTexture(tex);
                    m_prims[index].ScheduleGroupForTerseUpdate();
                }
            }
        }

		void OnTimer(object source, ElapsedEventArgs e)
        {
            UpdateMatrix();
        }

        void UpdateMatrix()
        {
            float[] newMatrix = new float[m_xCells * m_yCells];
            int rowabove;
            int rowbelow;
            int colleft;
            int colright;
            int xMaxIndex = m_xCells - 1;
            int yMaxIndex = m_yCells - 1;
            for (int y=0; y<m_yCells; y++)
            {
				if (y == 0)
                {
					rowabove = y + 1;
					rowbelow = yMaxIndex;
				}
				else if (y == yMaxIndex)
                {
					rowabove = 0;
					rowbelow = y - 1;
				}
				else
                {
					rowabove = y + 1;
					rowbelow = y - 1;
				}
				for (int x=0; x<m_xCells; x++)
                {
                    if (x == 0)
                    {
						colright = x + 1;
						colleft = xMaxIndex;
					}
					else if (x == xMaxIndex)
                    {
						colright = 0;
						colleft = x - 1;
					}
					else
                    {
						colright = x + 1;
						colleft = x - 1;
					}
                    int index = y * m_xCells + x;
                    float neighboraverage = ((m_matrix[rowbelow * m_xCells + colleft] +
                                              m_matrix[y * m_xCells + colleft] +
                                              m_matrix[rowabove * m_xCells + colleft] +
                                              m_matrix[rowbelow * m_xCells + x] +
                                              m_matrix[index] +
                                              m_matrix[rowabove * m_xCells + x] +
                                              m_matrix[rowbelow * m_xCells + colright] +
                                              m_matrix[y * m_xCells + colright] +
                                              m_matrix[rowabove * m_xCells + colright]
                                             ) / 9);
					float newCellValue = (neighboraverage + m_offset) % 1.0f;
                    newMatrix[index] = newCellValue;
					Primitive.TextureEntry tex = m_prims[index].RootPart.Shape.Textures;
					Color4 texcolor = new Color4(newCellValue, newCellValue, newCellValue, 1.0f);
					tex.DefaultTexture.RGBA = texcolor;
                    m_prims[index].RootPart.UpdateTexture(tex);
                    m_prims[index].ScheduleGroupForTerseUpdate();
				}
			}
	        Array.Copy(newMatrix, m_matrix, m_xCells * m_yCells);
        }
    }
}
