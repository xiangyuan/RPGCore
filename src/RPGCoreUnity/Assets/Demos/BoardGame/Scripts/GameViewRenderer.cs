﻿using RPGCore.Behaviour;
using RPGCore.Demo.BoardGame;
using System.Collections.Generic;
using UnityEngine;

namespace RPGCoreUnity.Demo.BoardGame
{
	public class GameViewRenderer : MonoBehaviour
	{
		public PlayerSelection ThisPlayerSelection;
		public SelectionRenderer SelectionRenderer;

		[Header("Grid")]
		public GameObject TilePrefab;

		private GameBoardRenderer[] Boards;

		public GameView Game;
		private LocalId Player1;
		private LocalId Player2;

		private TileRenderer CurrentlyHoveredTile;
		private TileRenderer DraggingStart;
		private bool IsDragging;

		public void OnTileHover(TileRenderer tileRenderer)
		{
			CurrentlyHoveredTile = tileRenderer;
		}

		public void OnTileUnhover(TileRenderer tileRenderer)
		{
			if (CurrentlyHoveredTile == tileRenderer)
			{
				CurrentlyHoveredTile = null;
			}
		}

		private void Update()
		{
			if (Input.GetMouseButtonDown(0))
			{
				if (CurrentlyHoveredTile != null)
				{
					IsDragging = true;
					DraggingStart = CurrentlyHoveredTile;
				}
			}
			if (Input.GetMouseButtonUp(0))
			{
				if (IsDragging)
				{
					IsDragging = false;
					DraggingStart = null;
				}
			}

			if (IsDragging && DraggingStart != null && CurrentlyHoveredTile != null)
			{
				ThisPlayerSelection.isSelected = true;

				int minPositionX = Mathf.Min(DraggingStart.Position.x, CurrentlyHoveredTile.Position.x);
				int minPositionY = Mathf.Min(DraggingStart.Position.y, CurrentlyHoveredTile.Position.y);
				int maxPositionX = Mathf.Max(DraggingStart.Position.x, CurrentlyHoveredTile.Position.x);
				int maxPositionY = Mathf.Max(DraggingStart.Position.y, CurrentlyHoveredTile.Position.y);

				int width = minPositionX - maxPositionX;
				int height = minPositionY - maxPositionY;

				if (width > 0)
				{
					width++;
				}
				else
				{
					width--;
				}

				if (height > 0)
				{
					height++;
				}
				else
				{
					height--;
				}

				width = Mathf.Abs(width);
				height = Mathf.Abs(height);

				ThisPlayerSelection.x = minPositionX;
				ThisPlayerSelection.y = minPositionY;
				ThisPlayerSelection.width = width;
				ThisPlayerSelection.height = height;
			}
		}

		private void Awake()
		{
			Player1 = LocalId.NewShortId();
			Player2 = LocalId.NewShortId();

			Game = new GameView()
			{
				CurrentPlayersTurn = 0,
				DeclaredResource = false,
				Players = new GamePlayer[]
				{
					new GamePlayer()
					{
						OwnerId = Player1,
						Board = new GameBoard(4, 4)
					},
					new GamePlayer()
					{
						OwnerId = Player2,
						Board = new GameBoard(4, 4)
					}
				}
			};
		}

		private void Start()
		{
			Boards = new GameBoardRenderer[Game.Players.Length];

			for (int i = 0; i < Game.Players.Length; i++)
			{
				var player = Game.Players[i];
				var playerHolder = new GameObject(player.OwnerId.ToString());
				playerHolder.transform.position = new Vector3(i * 5.0f, 0.0f, 0.0f);

				var boardRenderer = playerHolder.AddComponent<GameBoardRenderer>();
				boardRenderer.TileRenderers = new TileRenderer[player.Board.Width, player.Board.Height];

				Boards[i] = boardRenderer;

				for (int x = 0; x < player.Board.Width; x++)
				{
					for (int y = 0; y < player.Board.Height; y++)
					{
						var tileClone = Instantiate(TilePrefab,
							Vector3.zero,
							Quaternion.LookRotation(Vector3.down),
							playerHolder.transform);

						tileClone.transform.localPosition = new Vector3(x + 0.5f, 0.0f, y + 0.5f);
						tileClone.name = $"{x},{y}";

						var tileRenderer = tileClone.GetComponent<TileRenderer>();
						boardRenderer.TileRenderers[x, y] = tileRenderer;

						var tile = player.Board[x, y];
						tileRenderer.Render(this, tile, new Integer2(x, y));
					}
				}
			}

			SelectionRenderer.Render(ThisPlayerSelection);

			StartCoroutine(Run());
		}

		private void UpdateRendering()
		{
			foreach (var board in Boards)
			{
				board.UpdateRendering();
			}
		}

		private IEnumerator<YieldInstruction> Run()
		{
			var actions = new GameViewAction[]
			{
				new DeclareResourceAction()
				{
					Client = Player1,
					ResourceIdentifier = "x"
				},
				new PlaceResourceAction()
				{
					Client = Player1,
					ResourceIdentifier = "x",
					ResourcePosition = new Integer2(1, 1)
				},
				new PlaceResourceAction()
				{
					Client = Player2,
					ResourceIdentifier = "x",
					ResourcePosition = new Integer2(3, 3)
				},
				new EndTurnAction()
				{
					Client = Player1
				},

				new DeclareResourceAction()
				{
					Client = Player2,
					ResourceIdentifier = "x"
				},
				new PlaceResourceAction()
				{
					Client = Player1,
					ResourceIdentifier = "x",
					ResourcePosition = new Integer2(0, 1)
				},
				new PlaceResourceAction()
				{
					Client = Player2,
					ResourceIdentifier = "x",
					ResourcePosition = new Integer2(2, 3)
				},
				new EndTurnAction()
				{
					Client = Player2
				},

				new DeclareResourceAction()
				{
					Client = Player1,
					ResourceIdentifier = "x"
				},
				new PlaceResourceAction()
				{
					Client = Player1,
					ResourceIdentifier = "x",
					ResourcePosition = new Integer2(1, 2)
				},
				new PlaceResourceAction()
				{
					Client = Player2,
					ResourceIdentifier = "x",
					ResourcePosition = new Integer2(2, 2)
				},
				new BuildBuildingAction()
				{
					Client = Player1,
					BuildingIdentifier = "building",
					BuildingPosition = new Integer2(3, 3),
					Offset = new Integer2(2, 1),
					Orientation = BuildingOrientation.MirrorXandY
				},
				new EndTurnAction()
				{
					Client = Player1
				}
			};

			UpdateRendering();

			foreach (var action in actions)
			{
				while (!Input.GetKeyDown(KeyCode.Space))
				{
					yield return null;
				}

				Debug.Log($"<b>{action.Client}</b>: Running action {action.GetType().Name}\n");

				Game.Apply(action);

				UpdateRendering();
				yield return null;
			}
		}
	}
}
