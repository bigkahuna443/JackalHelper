﻿using System;
using System.Linq;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.JackalHelper.Entities
{
	[CustomEntity(
		"JackalHelper/CardinalBumper",
		"JackalHelper/LinkedCardinalBumper = LoadLinked")]
	[Tracked]
	public class CardinalBumper : Entity
	{
		public static ParticleType P_Ambience;
		public static ParticleType P_Launch;
		public static ParticleType P_FireAmbience;
		public static ParticleType P_FireHit;

		public bool Linked = false;

		public bool alwaysBumperBoost;
		public bool wobble;

		public Vector2 startPos;
		public Vector2 goal = Vector2.Zero;

		public int index;
		private Vector2[] positionNodes;

		public bool travelling = true;

		private float respawnTimer;

		// Graphics
		public Sprite[] outlines;
		private Sprite sprite;

		private VertexLight light;

		private BloomPoint bloom;

		public static Entity LoadLinked(Level level, LevelData levelData, Vector2 offset, EntityData data)
		{
			return new CardinalBumper(data, offset) { Linked = true };
		}

		public CardinalBumper(Vector2 position, Vector2[] nodes, bool alwaysBumperBoost, bool wobble, string spriteDirectory) 
			: base(position)
		{
			this.alwaysBumperBoost = alwaysBumperBoost;
			this.wobble = wobble;

			startPos = Position;

			Collider = new Hitbox(20f, 20f, -10f, -10f);
			Collider leftCollider = new Hitbox(1f, 16f, -11f, -8f);
			Collider rightCollider = new Hitbox(1f, 16f, 10f, -8f);
			Collider topCollider = new Hitbox(16f, 1f, -8f, -11f);
			Collider bottomCollider = new Hitbox(16f, 1f, -8f, 10f);

			Add(new PlayerCollider(OnPlayerLeft, leftCollider));
			Add(new PlayerCollider(OnPlayerRight, rightCollider));
			Add(new PlayerCollider(OnPlayerTop, topCollider));
			Add(new PlayerCollider(OnPlayerBottom, bottomCollider));

			positionNodes = nodes;

			outlines = new Sprite[positionNodes.Length];
			for (int i = 0; i < positionNodes.Length; i++)
			{
				outlines[i] = new Sprite(GFX.Game, "objects/" + spriteDirectory + "/outline");
				outlines[i].Position = nodes[i];
				outlines[i].Visible = false;

			}
			goal = positionNodes[0];
			index = 0;

			Add(sprite = JackalModule.spriteBank.Create(spriteDirectory));
			Add(light = new VertexLight(Color.Teal, 1f, 16, 32));
			Add(new VertexLight(Color.Orange, 1f, 16, 32));
			Add(bloom = new BloomPoint(0.5f, 16f));
			Add(outlines);
			if (goal != Vector2.Zero)
			{
				UpdatePosition(goal);
			}
		}

		public CardinalBumper(EntityData data, Vector2 offset) 
			: this(data.Position + offset, data.NodesWithPosition(offset), data.Bool("alwaysBumperBoost", defaultValue: false), data.Bool("wobble", defaultValue: false), data.Attr("spriteDirectory", defaultValue: "bumperCardinal"))
		{ }

		public override void Added(Scene scene)
		{
			base.Added(scene);
			sprite.Visible = true;
		}

		private void UpdatePosition(Vector2 position)
		{
			// COLOURSOFNOISE: You really need to use Engine.DeltaTime for any movement/update stuff
			Vector2 path = position - Position;
			if (Math.Abs(path.X) < 0.5f && Math.Abs(path.Y) < 0.5f)
				// Snap alignment to the grid when resting
				Position = position;
			else
				Position += path / 0.5f * Engine.DeltaTime;
		}

		public override void Update()
		{
			base.Update();
			Collidable = Vector2.Distance(Position, positionNodes[index]) < 24;
			if (respawnTimer > 0f)
			{
				respawnTimer -= Engine.DeltaTime;
				if (respawnTimer <= 0f)
				{
					light.Visible = true;
					bloom.Visible = true;
					sprite.Play("on");

					Audio.Play("event:/game/06_reflection/pinballbumper_reset", Position);

				}
			}
			else if (Scene.OnInterval(0.05f))
			{
				ParticleType type = (P_Ambience);
				if (type != null) // COLOURSOFNOISE: the particle type is never assigned...
				{
					float direction = Calc.Random.NextAngle();
					float length = (8);
					SceneAs<Level>().Particles.Emit(type, 1, Center + Calc.AngleToVector(direction, length), Vector2.One * 2f, direction);
				}
			}

			if (goal == positionNodes[positionNodes.Length - 1])
			{
				Array.Reverse(positionNodes);
				index = 0;
				goal = positionNodes[index];
			}

			if (goal != Vector2.Zero)
			{
				UpdatePosition(goal);
			}
		}

		private void OnPlayer(Player player, Vector2 launchVector)
		{
			if (respawnTimer <= 0f)
			{
				Audio.Play("event:/game/06_reflection/pinballbumper_hit", Position);
				respawnTimer = 0.7f;
				CardinalLaunch(player, launchVector);
				player.StateMachine.State = Player.StNormal;
				sprite.Play("hit", restart: true);
				light.Visible = false;
				bloom.Visible = false;
				SceneAs<Level>().Displacement.AddBurst(Center, 0.3f, 8f, 32f, 0.8f);
			}
		}

		private void OnPlayerLeft(Player player)
		{
			OnPlayer(player, new Vector2(-1f, -0.4f));
		}

		private void OnPlayerRight(Player player)
		{
			OnPlayer(player, new Vector2(1f, -0.4f));
		}

		private void OnPlayerTop(Player player)
		{
			OnPlayer(player, new Vector2(0f, -1f));
		}

		private void OnPlayerBottom(Player player)
		{
			OnPlayer(player, new Vector2(0f, 1f));
		}

		public void CardinalLaunch(Player player, Vector2 launchVector)
		{
			DynData<Player> dyn = new DynData<Player>(player);
			dyn.Set("varJumpTimer", 0f);

			player.StateMachine.State = Player.StLaunch;
			player.AutoJump = true;

			Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
			Celeste.Freeze(0.1f);

			Vector2 speed = launchVector * 350f;
			if (!player.Inventory.NoRefills)
			{
				player.RefillDash();
			}
			if (alwaysBumperBoost)
			{
				player.Speed *= 1.4f;
			}
			SlashFx.Burst(Center, speed.Angle());
			player.RefillStamina();
			player.Speed = speed;

			if (Linked)
			{
				foreach (CardinalBumper bumper in Scene.Tracker.GetEntities<CardinalBumper>())
				{
					if (bumper.Linked)
					{
						bumper.NextNode();
					}
				}
			}
			else
			{
				NextNode();
			}
		}

		private void NextNode()
		{
			travelling = true;
			if (positionNodes.Length > 1)
			{
				if (Vector2.Distance(Position, positionNodes[index]) < 24f)
				{
					index += 1;
					goal = positionNodes[index];
				}
			}
		}

		public override void DebugRender(Camera camera)
		{
			// COLOURSOFNOISE: Should be implemented to show the exact angle limits
			base.DebugRender(camera);
		}
	}
}

