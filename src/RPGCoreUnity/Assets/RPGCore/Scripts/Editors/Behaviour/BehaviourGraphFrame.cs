﻿using Newtonsoft.Json.Linq;
using RPGCore.Behaviour;
using RPGCore.Behaviour.Editor;
using RPGCore.Behaviour.Manifest;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RPGCore.Unity.Editors
{
	public class BehaviourGraphFrame : WindowFrame
	{
		public static Dictionary<string, GraphTypeInformation> StyleLookup = new Dictionary<string, GraphTypeInformation>()
		{
			["Single"] = new GraphTypeInformation()
			{
				ConnectionColor = new Color(0.65f, 0.65f, 0.65f),
				SocketColor = new Color(0.65f, 0.65f, 0.65f),
			},
			["Int32"] = new GraphTypeInformation()
			{
				ConnectionColor = new Color(0.85f, 0.85f, 0.85f),
				SocketColor = new Color(0.85f, 0.85f, 0.85f),
			},
			["DemoPlayer"] = new GraphTypeInformation()
			{
				ConnectionColor = new Color(0.6f, 0.6f, 0.85f),
				SocketColor = new Color(0.6f, 0.6f, 0.85f),
			},
			["Error"] = new GraphTypeInformation()
			{
				ConnectionColor = new Color(0.85f, 0.6f, 0.6f),
				SocketColor = new Color(0.85f, 0.6f, 0.6f),
			}
		};

		public BehaviourEditorView View;

		private Event CurrentEvent;
		private GUIStyle NodePadding;

		public override void OnEnable()
		{
			NodePadding = new GUIStyle()
			{
				padding = new RectOffset()
				{
					left = 4,
					right = 4
				}
			};
		}

		public override void OnGUI()
		{
			if (View == null)
			{
				View = new BehaviourEditorView();
			}

			CurrentEvent = Event.current;

			DrawBackground(Position, View.PanPosition);
			DrawNodes();
			DrawConnections();
			HandleInput();
		}

		private void DrawNodes()
		{
			if (View.Session == null)
			{
				return;
			}
			var graphEditorNodes = View.GraphField["Nodes"];

			// Draw Nodes
			foreach (var node in graphEditorNodes)
			{
				var nodeEditor = node["Editor"];
				var nodeEditorPosition = nodeEditor["Position"];

				var nodeRect = new Rect(
					View.PanPosition.x + nodeEditorPosition["x"].GetValue<int>(),
					View.PanPosition.y + nodeEditorPosition["y"].GetValue<int>(),
					220,
					1000
				);

				GUILayout.BeginArea(nodeRect);

				var finalRect = EditorGUILayout.BeginVertical(NodePadding);
				// finalRect.xMax -= 2;
				finalRect.yMax += 4;
				if (CurrentEvent.type == EventType.Repaint)
				{
					GUI.skin.window.Draw(finalRect,
						false, View.Selection.Contains(node.Name), false, false);
				}

				var nodeType = node["Type"];
				string nodeTypeString = nodeType.GetValue<string>();
				string nodeHeader = nodeTypeString?.Substring(nodeTypeString.LastIndexOf(".") + 1) ?? "Unknown";
				EditorGUILayout.LabelField(nodeHeader, EditorStyles.boldLabel);

				var nodeData = node["Data"];

				float originalLabelWidth = EditorGUIUtility.labelWidth;
				EditorGUIUtility.labelWidth = 115;
				EditorGUI.indentLevel++;
				foreach (var childField in nodeData)
				{
					RPGCoreEditor.DrawField(childField);
				}
				EditorGUI.indentLevel--;
				EditorGUIUtility.labelWidth = originalLabelWidth;

				EditorGUILayout.EndVertical();
				GUILayout.EndArea();

				if (CurrentEvent.type == EventType.MouseDown)
				{
					var globalFinalRect = new Rect(
						nodeRect.x,
						nodeRect.y,
						finalRect.width,
						finalRect.height
					);

					if (globalFinalRect.Contains(CurrentEvent.mousePosition))
					{
						View.Selection.Clear();
						View.Selection.Add(node.Name);

						View.CurrentMode = BehaviourEditorView.ControlMode.NodeDragging;
						GUI.UnfocusWindow();
						GUI.FocusControl("");

						CurrentEvent.Use();
					}
				}
			}
		}

		public void DrawConnections()
		{
			if (View.Session == null)
			{
				return;
			}

			if (CurrentEvent.type != EventType.Repaint
				&& CurrentEvent.type != EventType.MouseDown
				&& CurrentEvent.type != EventType.MouseUp)
			{
				return;
			}

			var graphEditorNodes = View.GraphField["Nodes"];

			// Foreach output
			foreach (var node in graphEditorNodes)
			{
				var nodeEditor = node["Editor"];
				var nodeEditorPosition = nodeEditor["Position"];

				float nodePositionX = nodeEditorPosition["x"].GetValue<int>() + View.PanPosition.x;
				float nodePositionY = nodeEditorPosition["y"].GetValue<int>() + View.PanPosition.y;

				var nodeData = node["Data"];

				// Foreach Output
				var nodeInfo = (NodeInformation)nodeData.Type;
				if (nodeInfo?.Outputs != null)
				{
					var outputSocketRect = new Rect(nodePositionX + 220, nodePositionY + 6, 20, 20);
					foreach (var output in nodeInfo.Outputs)
					{
						if (CurrentEvent.type == EventType.Repaint)
						{
							if (!StyleLookup.TryGetValue(output.Value.Type, out var connectionStyle))
							{
								connectionStyle = StyleLookup["Error"];
								Debug.LogWarning($"Couldn't find a style for connections of type \"{output.Value.Type}\".");
							}

							var originalColor = GUI.color;
							GUI.color = connectionStyle.SocketColor;
							GUI.DrawTexture(outputSocketRect, BehaviourGraphResources.Instance.OutputSocket);
							GUI.color = originalColor;
						}
						else if (CurrentEvent.type == EventType.MouseDown && outputSocketRect.Contains(CurrentEvent.mousePosition))
						{
							var outputId = new LocalPropertyId(new LocalId(node.Name), output.Key);
							View.BeginConnectionFromOutput(outputId);

							GUI.UnfocusWindow();
							GUI.FocusControl("");

							CurrentEvent.Use();
						}
						else if (CurrentEvent.type == EventType.MouseUp && outputSocketRect.Contains(CurrentEvent.mousePosition))
						{
							if (View.CurrentMode == BehaviourEditorView.ControlMode.CreatingConnection)
							{
								if (!View.IsOutputSocket)
								{
									var thisOutputSocket = new LocalPropertyId(new LocalId(node.Name), output.Key);

									View.ConnectionInput.SetValue(thisOutputSocket);
									View.ConnectionInput.ApplyModifiedProperties();
									View.CurrentMode = BehaviourEditorView.ControlMode.None;

									GUI.UnfocusWindow();
									GUI.FocusControl("");

									CurrentEvent.Use();
								}
							}
						}

						outputSocketRect.y += outputSocketRect.height + 4;
					}
				}

				// Foreach Input
				foreach (var childField in nodeData)
				{
					if (childField.Field.Type != "InputSocket")
					{
						continue;
					}
					var inputFieldFeature = childField.GetOrCreateFeature<FieldFeature>();

					inputFieldFeature.GlobalRenderedPosition = new Rect(
						inputFieldFeature.LocalRenderedPosition.x + nodePositionX - 4,
						inputFieldFeature.LocalRenderedPosition.y + nodePositionY,
						inputFieldFeature.LocalRenderedPosition.width,
						inputFieldFeature.LocalRenderedPosition.height);

					var inputSocketRect = inputFieldFeature.InputSocketPosition;

					if (CurrentEvent.type == EventType.MouseDown && inputSocketRect.Contains(CurrentEvent.mousePosition))
					{
						View.BeginConnectionFromInput(childField, node.Name);

						GUI.UnfocusWindow();
						GUI.FocusControl("");

						CurrentEvent.Use();
					}
					else if (CurrentEvent.type == EventType.MouseUp && inputSocketRect.Contains(CurrentEvent.mousePosition))
					{
						if (View.CurrentMode == BehaviourEditorView.ControlMode.CreatingConnection)
						{
							if (View.IsOutputSocket)
							{
								childField.SetValue(View.ConnectionOutput);
								childField.ApplyModifiedProperties();
								View.CurrentMode = BehaviourEditorView.ControlMode.None;

								GUI.UnfocusWindow();
								GUI.FocusControl("");

								CurrentEvent.Use();
							}
						}
					}
					else if (CurrentEvent.type == EventType.Repaint)
					{
						if (!StyleLookup.TryGetValue("Error", out var inputStyle))
						{
							inputStyle = StyleLookup["Error"];
							Debug.LogWarning($"Couldn't find a style for connections of type \"Error\".");
						}

						var originalColor = GUI.color;
						GUI.color = inputStyle.SocketColor;
						GUI.DrawTexture(inputSocketRect, BehaviourGraphResources.Instance.InputSocket);
						GUI.color = originalColor;

						var thisInputConnectedTo = childField.GetValue<LocalPropertyId>();
						if (thisInputConnectedTo != LocalPropertyId.None)
						{
							bool isFound = false;
							Rect otherOutputSocketRect = default;
							SocketInformation outputSocketInformation = default;

							foreach (var otherNode in graphEditorNodes)
							{
								var otherNodeEditor = otherNode["Editor"];
								var otherNodeEditorPosition = otherNodeEditor["Position"];

								float otherNodePositionX = otherNodeEditorPosition["x"].GetValue<int>() + View.PanPosition.x;
								float otherNodePositionY = otherNodeEditorPosition["y"].GetValue<int>() + View.PanPosition.y;

								var otherNodeData = otherNode["Data"];

								// Foreach Output
								otherOutputSocketRect = new Rect(otherNodePositionX + 220, otherNodePositionY + 6, 20, 20);
								var otherNodeInfo = (NodeInformation)otherNodeData.Type;
								foreach (var output in otherNodeInfo.Outputs)
								{
									var otherOutputId = new LocalPropertyId(new LocalId(otherNode.Name), output.Key);

									if (otherOutputId == thisInputConnectedTo)
									{
										isFound = true;
										outputSocketInformation = output.Value;
										break;
									}

									otherOutputSocketRect.y += otherOutputSocketRect.height + 4;
								}
								if (isFound)
								{
									break;
								}
							}
							if (isFound)
							{
								var start = new Vector3(otherOutputSocketRect.x, otherOutputSocketRect.center.y);
								var end = new Vector3(inputFieldFeature.GlobalRenderedPosition.x, inputFieldFeature.GlobalRenderedPosition.center.y);
								var startDir = new Vector3(1, 0);
								var endDir = new Vector3(-1, 0);

								if (!StyleLookup.TryGetValue(outputSocketInformation.Type, out var connectionStyle))
								{
									connectionStyle = StyleLookup["Error"];
									Debug.LogWarning($"Couldn't find a style for connections of type \"{outputSocketInformation.Type}\".");
								}

								DrawConnection(start, end, startDir, endDir, connectionStyle.ConnectionColor);
							}
						}
					}
				}
			}

			// Draw active connection
			if (View.CurrentMode == BehaviourEditorView.ControlMode.CreatingConnection)
			{
				if (View.IsOutputSocket)
				{
					// Draw Nodes
					bool isFound = false;
					Rect outputRect = default;
					SocketInformation outputSocketInformation = default;

					foreach (var node in graphEditorNodes)
					{
						var nodeEditor = node["Editor"];
						var nodeEditorPosition = nodeEditor["Position"];

						float nodePositionX = nodeEditorPosition["x"].GetValue<int>() + View.PanPosition.x;
						float nodePositionY = nodeEditorPosition["y"].GetValue<int>() + View.PanPosition.y;

						var nodeData = node["Data"];

						// Foreach Output
						var nodeInfo = (NodeInformation)nodeData.Type;
						outputRect = new Rect(nodePositionX + 220, nodePositionY + 6, 20, 20);
						foreach (var output in nodeInfo.Outputs)
						{
							var otherOutputId = new LocalPropertyId(new LocalId(node.Name), output.Key);

							if (otherOutputId == View.ConnectionOutput)
							{
								isFound = true;
								outputSocketInformation = output.Value;
								break;
							}

							outputRect.y += outputRect.height + 4;
						}
						if (isFound)
						{
							break;
						}
					}

					if (isFound)
					{
						var start = new Vector3(outputRect.x, outputRect.center.y);
						var end = new Vector3(CurrentEvent.mousePosition.x, CurrentEvent.mousePosition.y);
						var startDir = new Vector3(1, 0);
						var endDir = new Vector3(-1, 0);

						if (!StyleLookup.TryGetValue(outputSocketInformation.Type, out var connectionStyle))
						{
							connectionStyle = StyleLookup["Error"];
							Debug.LogWarning($"Couldn't find a style for connections of type \"{outputSocketInformation.Type}\".");
						}

						DrawConnection(start, end, startDir, endDir, connectionStyle.ConnectionColor);
					}
				}
				else
				{
					if (!StyleLookup.TryGetValue("Error", out var inputStyle))
					{
						inputStyle = StyleLookup["Error"];
						Debug.LogWarning($"Couldn't find a style for connections of type \"Error\".");
					}

					var startFieldFeature = View.ConnectionInput.GetOrCreateFeature<FieldFeature>();

					var inputSocketRect = startFieldFeature.InputSocketPosition;

					var start = new Vector3(CurrentEvent.mousePosition.x, CurrentEvent.mousePosition.y);
					var end = new Vector3(inputSocketRect.xMax, inputSocketRect.center.y);
					var startDir = new Vector3(1, 0);
					var endDir = new Vector3(-1, 0);

					DrawConnection(start, end, startDir, endDir, inputStyle.ConnectionColor);
				}
			}
		}

		private static void DrawConnection(Vector3 start, Vector3 end, Vector3 startDir, Vector3 endDir, Color connectionColour)
		{
			float distance = Vector3.Distance(start, end);
			var startTan = start + (startDir * distance * 0.5f);
			var endTan = end + (endDir * distance * 0.5f);

			Handles.DrawBezier(start, end, startTan, endTan, connectionColour,
				BehaviourGraphResources.Instance.SmallConnection, 10);
		}

		private void HandleInput()
		{
			if (CurrentEvent.type == EventType.MouseUp)
			{
				switch (View.CurrentMode)
				{
					case BehaviourEditorView.ControlMode.NodeDragging:
						CurrentEvent.Use();

						foreach (string selectedNode in View.Selection)
						{
							var pos = View.GraphField["Nodes"][selectedNode]["Editor"]["Position"];

							var posX = pos["x"];
							posX.ApplyModifiedProperties();

							var posY = pos["y"];
							posY.ApplyModifiedProperties();
						}
						View.CurrentMode = BehaviourEditorView.ControlMode.None;
						break;

					case BehaviourEditorView.ControlMode.ViewDragging:
						View.CurrentMode = BehaviourEditorView.ControlMode.None;
						break;

					case BehaviourEditorView.ControlMode.CreatingConnection:
						View.CurrentMode = BehaviourEditorView.ControlMode.None;
						break;
				}
				Window.Repaint();
			}
			else if (CurrentEvent.type == EventType.KeyDown)
			{
				if (CurrentEvent.keyCode == KeyCode.Space)
				{
					var graphEditorNodes = View.GraphField["Nodes"];

					string newId = LocalId.NewId().ToString();
					graphEditorNodes.Add(newId);

					var newNode = graphEditorNodes[newId];
					var nodeData = new JObject();
					newNode.SetValue(new SerializedNode()
					{
						Type = "RPGCore.Demo.Inventory.Nodes.AddNode",
						Data = nodeData
					});
					newNode.ApplyModifiedProperties();
					Window.Repaint();
				}
				else if (CurrentEvent.keyCode == KeyCode.Delete)
				{
					var graphEditorNodes = View.GraphField["Nodes"];

					foreach (string node in View.Selection)
					{
						graphEditorNodes.Remove(node);
					}
				}
			}
			else if (CurrentEvent.type == EventType.MouseDrag)
			{
				switch (View.CurrentMode)
				{
					case BehaviourEditorView.ControlMode.NodeDragging:
						foreach (string selectedNode in View.Selection)
						{
							var pos = View.GraphField["Nodes"][selectedNode]["Editor"]["Position"];

							var posX = pos["x"];
							posX.SetValue(posX.GetValue<int>() + ((int)CurrentEvent.delta.x));

							var posY = pos["y"];
							posY.SetValue(posY.GetValue<int>() + ((int)CurrentEvent.delta.y));
						}
						break;

					case BehaviourEditorView.ControlMode.ViewDragging:
						View.PanPosition += CurrentEvent.delta;
						break;
				}
				Window.Repaint();
			}
			else if (CurrentEvent.type == EventType.MouseDown)
			{
				if (Position.Contains(CurrentEvent.mousePosition))
				{
					GUI.UnfocusWindow();
					GUI.FocusControl("");

					View.CurrentMode = BehaviourEditorView.ControlMode.ViewDragging;

					CurrentEvent.Use();
					Window.Repaint();
				}
			}
		}

		private void DrawBackground(Rect backgroundRect, Vector2 viewPosition)
		{
			if (CurrentEvent.type != EventType.Repaint)
			{
				return;
			}

			if (View.Session == null)
			{
				EditorGUI.LabelField(backgroundRect, "No Graph Selected", BehaviourGUIStyles.Instance.informationTextStyle);
				return;
			}

#if HOVER_EFFECTS
			if (dragging_IsDragging)
			{
				EditorGUIUtility.AddCursorRect (backgroundRect, MouseCursor.Pan);
			}
#endif
			float gridScale = 1.5f;

			/*if (Application.isPlaying)
				EditorGUI.DrawRect (screenRect, new Color (0.7f, 0.7f, 0.7f));*/

			var originalTintColour = GUI.color;
			GUI.color = new Color(1.0f, 1.0f, 1.0f, 0.3f);

			DrawImageTiled(backgroundRect, BehaviourGraphResources.Instance.WindowBackground, viewPosition, gridScale);

			GUI.color = originalTintColour;

			if (Application.isPlaying)
			{
				var runtimeInfo = new Rect(backgroundRect);
				runtimeInfo.yMin = runtimeInfo.yMax - 48;
				EditorGUI.LabelField(runtimeInfo, "Playmode Enabled: You may change values but you can't edit connections",
					BehaviourGUIStyles.Instance.informationTextStyle);
			}
		}

		private void DrawImageTiled(Rect rect, Texture2D texture, Vector2 positon, float zoom = 0.8f)
		{
			if (texture == null)
			{
				return;
			}

			if (CurrentEvent.type != EventType.Repaint)
			{
				return;
			}

			var tileOffset = new Vector2((-positon.x / texture.width) * zoom, (positon.y / texture.height) * zoom);

			var tileAmount = new Vector2(Mathf.Round(rect.width * zoom) / texture.width,
				Mathf.Round(rect.height * zoom) / texture.height);

			tileOffset.y -= tileAmount.y;
			GUI.DrawTextureWithTexCoords(rect, texture, new Rect(tileOffset, tileAmount), true);
		}
	}
}
