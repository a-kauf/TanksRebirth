using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using TanksRebirth.Enums;
using System.Linq;
using TanksRebirth.Internals.Common.GameInput;
using Microsoft.Xna.Framework.Input;
using TanksRebirth.Internals.Common.Utilities;
using TanksRebirth.Internals;
using Microsoft.Xna.Framework.Audio;
using TanksRebirth.Internals.Common;
using TanksRebirth.GameContent.GameMechanics;
using TanksRebirth.Internals.Common.Framework.Audio;
using TanksRebirth.Internals.Common.Framework.Input;
using TanksRebirth.Graphics;
using TanksRebirth.Net;
using TanksRebirth.GameContent.Systems;
using FontStashSharp;
using TanksRebirth.GameContent.Properties;
using TanksRebirth.GameContent.UI;
using TanksRebirth.GameContent.ID;

namespace TanksRebirth.GameContent
{
    public class PlayerTank : Tank
    {

        #region The Rest
        public static int MyTeam;

        public static int StartingLives = 3;
        public static int Lives = 0;

        // public static Dictionary<PlayerType, Dictionary<TankTier, int>> TanksKillDict = new(); // this campaign only!
        public static Dictionary<int, int> TankKills = new(); // this campaign only!

        public struct DeterministicPlayerStats {
            public int ShellsShotThisCampaign;
            public int ShellHitsThisCampaign;
            public int MinesLaidThisCampaign;
            public int MineHitsThisCampaign;
            public int SuicidesThisCampaign; // self-damage this campaign?
        }

        public static DeterministicPlayerStats PlayerStatistics;

        public static bool _drawShotPath;

        public static int KillCount = 0;
        
        private bool playerControl_isBindPressed;

        public int PlayerId { get; }
        public int PlayerType { get; }

        private Texture2D _tankTexture;
        private static Texture2D _shadowTexture;

        public static Keybind controlUp = new("Up", Keys.W);
        public static Keybind controlDown = new("Down", Keys.S);
        public static Keybind controlLeft = new("Left", Keys.A);
        public static Keybind controlRight = new("Right", Keys.D);
        public static Keybind controlMine = new("Place Mine", Keys.Space);
        public static Keybind controlFirePath = new("Draw Shot Path", Keys.Q);
        public static GamepadBind FireBullet = new("Fire Bullet", Buttons.RightTrigger);
        public static GamepadBind PlaceMine = new("Place Mine", Buttons.A);

        public Vector2 oldPosition;

        private bool _isPlayerModel;

        #endregion
        public void SwapTankTexture(Texture2D texture) => _tankTexture = texture;

        public PlayerTank(int playerType, bool isPlayerModel = true, int copyTier = TankID.None)
        {
            Model = GameResources.GetGameResource<Model>(isPlayerModel ? "Assets/tank_p" : "Assets/tank_e");
            if (copyTier == TankID.None)
            {
                _tankTexture = Assets[$"tank_" + PlayerID.Collection.GetKey(playerType).ToLower()];
                ApplyDefaults(ref Properties);
            }
            else
            {
                _tankTexture = Assets[$"tank_" + TankID.Collection.GetKey(copyTier).ToLower()];
                var dummy = new AITank(copyTier, default, true, false, false);

                // ugly hardcode fix lol - prevents nantuple instead of triple bounces
                // maybe inefficient on memory
                // TODO: should probably be written better


				// TODO: hardcode hell. bad. suck. balls
                if (Difficulties.Types["BulletHell"])
                    dummy.Properties.RicochetCount /= 3;

                Properties = dummy.Properties;

                dummy.Remove();
            }

            _isPlayerModel = isPlayerModel;

            //CannonMesh = Model.Meshes["Cannon"];

            //boneTransforms = new Matrix[Model.Bones.Count];

            _shadowTexture = GameResources.GetGameResource<Texture2D>("Assets/textures/tank_shadow");

            PlayerType = playerType;

            if (playerType == PlayerID.Red)
                Properties.ShootPitch = 0.1f;

            Team = TeamID.Red;

            Dead = true;

            int index = Array.IndexOf(GameHandler.AllPlayerTanks, GameHandler.AllAITanks.First(tank => tank is null));

            PlayerId = index;

            GameHandler.AllPlayerTanks[index] = this;

            int index2 = Array.IndexOf(GameHandler.AllTanks, GameHandler.AllTanks.First(tank => tank is null));

            WorldId = index2;

            GameHandler.AllTanks[index2] = this;

            base.Initialize();
        }

        public override void ApplyDefaults(ref TankProperties properties)
        {
            properties.ShellCooldown = 5;
            properties.ShootStun = 5;
            properties.ShellSpeed = 3f; // 3f
            properties.MaxSpeed = 1.8f; // 1.8
            properties.RicochetCount = 1; // 1
            properties.ShellLimit = 5;
            properties.MineLimit = 2;
            properties.MineStun = 8;
            properties.Invisible = false;
            properties.Acceleration = 0.3f;
            properties.Deceleration = 0.6f;
            properties.TurningSpeed = 0.1f;
            properties.MaximalTurn = MathHelper.ToRadians(10); // normally it's 10 degrees, but we want to make it easier for keyboard players.

            properties.ShellType = ShellID.Player;

            properties.ShellHoming = new();

            properties.DestructionColor = PlayerType switch
            {
                PlayerID.Blue => Color.Blue,
                PlayerID.Red => Color.Red,
                _ => throw new Exception("What did you do?")
            };
            base.ApplyDefaults(ref properties);
        }

        public override void Update()
        {
            /*if (Input.KeyJustPressed(Keys.P))
                foreach (var m in TankDeathMark.deathMarks)
                    m?.ResurrectTank();*/
            // FIXME: reference?

            // pi/2 = up
            // 0 = down
            // pi/4 = right
            // 3/4pi = left

            base.Update();

            //CannonMesh.ParentBone.Transform = Matrix.CreateRotationY(TurretRotation + TankRotation + (Flip ? MathHelper.Pi : 0));
            //Model.Root.Transform = World;

            //Model.CopyAbsoluteBoneTransformsTo(boneTransforms);

            if (GameProperties.InMission)
            {
                if (TargetTankRotation - TankRotation >= MathHelper.PiOver2)
                {
                    TankRotation += MathHelper.Pi;
                    Flip = !Flip;
                }
                else if (TargetTankRotation - TankRotation <= -MathHelper.PiOver2)
                {
                    TankRotation -= MathHelper.Pi;
                    Flip = !Flip;
                }
            }

            if (IsIngame) {
                if (Client.IsConnected())
                    ChatSystem.SendMessage($"PlayerId: {PlayerId} | ClientId: {NetPlay.CurrentClient.Id}", Color.White);
                if (NetPlay.IsClientMatched(PlayerId) && !IntermissionSystem.IsAwaitingNewMission) {
                    if (!Difficulties.Types["ThirdPerson"]) {
                        Vector3 mouseWorldPos = GameUtils.GetWorldPosition(GameUtils.MousePosition, -11f);
                        if (!LevelEditor.Active)
                            TurretRotation = (-(new Vector2(mouseWorldPos.X, mouseWorldPos.Z) - Position).ToRotation()) + MathHelper.PiOver2;
                        else
                            TurretRotation = TankRotation;
                    }
                    else {
                        //Mouse.SetPosition(Input.CurrentMouseSnapshot.X, GameUtils.WindowHeight / 2);
                        if (Input.CurrentMouseSnapshot.X >= GameUtils.WindowWidth)
                            Mouse.SetPosition(1, Input.CurrentMouseSnapshot.Y);
                        if (Input.CurrentMouseSnapshot.X <= 0)
                            Mouse.SetPosition(GameUtils.WindowWidth - 1, Input.CurrentMouseSnapshot.Y);
                        //Mouse.SetPosition((int)GameUtils.WindowCenter.X, (int)GameUtils.WindowCenter.Y);
                        TurretRotation += -TankGame.MouseVelocity.X / 312; // terry evanswood
                    }
                }

                if (GameProperties.InMission && !LevelEditor.Active) {
                    if (CurShootStun <= 0 && CurMineStun <= 0) {
                        if (!Properties.Stationary) {
                            if (NetPlay.IsClientMatched(PlayerId)) {
                                if (Input.CurrentGamePadSnapshot.IsConnected)
                                    ControlHandle_ConsoleController();
                                else
                                    ControlHandle_Keybinding();
                            }
                        }
                    }
                }

                if (GameProperties.InMission && !LevelEditor.Active) {
                    if (NetPlay.IsClientMatched(PlayerId)) {
                        if (Input.CanDetectClick())
                            Shoot();

                        if (!Properties.Stationary)
                            UpdatePlayerMovement();
                    }
                }
            }

            timeSinceLastAction++;

            playerControl_isBindPressed = false;

            //if (Client.IsConnected() && IsIngame)
            //Client.SyncPlayer(this);

            oldPosition = Position;
        }

        public override void Remove()
        {
            Dead = true;
            GameHandler.AllPlayerTanks[PlayerId] = null;
            GameHandler.AllTanks[WorldId] = null;
            base.Remove();
        }

        public override void Shoot() {
            PlayerStatistics.ShellsShotThisCampaign++;
            base.Shoot();
        }

        public override void LayMine() {
            PlayerStatistics.MinesLaidThisCampaign++;
            base.LayMine();
        }

        private void ControlHandle_ConsoleController()
        {

            var leftStick = Input.CurrentGamePadSnapshot.ThumbSticks.Left;
            var rightStick = Input.CurrentGamePadSnapshot.ThumbSticks.Right;
            var dPad = Input.CurrentGamePadSnapshot.DPad;

            var treadPlaceTimer = (int)Math.Round(14 / Velocity.Length()) != 0 ? (int)Math.Round(14 / Velocity.Length()) : 1;

            var preterbedVelocity = new Vector2(leftStick.X, -leftStick.Y);

            var rotationMet = TankRotation > TargetTankRotation - Properties.MaximalTurn && TankRotation < TargetTankRotation + Properties.MaximalTurn;

            if (!rotationMet)
            {
                if (TankGame.UpdateCount % treadPlaceTimer == 0)
                    LayFootprint(Properties.TrackType == TrackID.Thick);
                Properties.Speed -= Properties.Deceleration * TankGame.DeltaTime;
                if (Properties.Speed < 0)
                    Properties.Speed = 0;
                IsTurning = true;
            }
            else
            {
                if (Difficulties.Types["ThirdPerson"])
                    preterbedVelocity = preterbedVelocity.RotatedByRadians(-TurretRotation + MathHelper.Pi);

                Properties.Speed += Properties.Acceleration * TankGame.DeltaTime;
                if (Properties.Speed > Properties.MaxSpeed)
                    Properties.Speed = Properties.MaxSpeed;
                
                if (leftStick.Length() > 0)
                {
                    playerControl_isBindPressed = true;
                }

                if (dPad.Down == ButtonState.Pressed)
                {
                    playerControl_isBindPressed = true;
                    preterbedVelocity.Y = 1;
                }
                if (dPad.Up == ButtonState.Pressed)
                {
                    playerControl_isBindPressed = true;
                    preterbedVelocity.Y = -1;
                }
                if (dPad.Left == ButtonState.Pressed)
                {
                    playerControl_isBindPressed = true;
                    preterbedVelocity.X = -1;
                }
                if (dPad.Right == ButtonState.Pressed)
                {
                    playerControl_isBindPressed = true;
                    preterbedVelocity.X = 1;
                }
            }

            var norm = Vector2.Normalize(preterbedVelocity);

            TargetTankRotation = norm.ToRotation() - MathHelper.PiOver2;

            TankRotation = GameUtils.RoughStep(TankRotation, TargetTankRotation, Properties.TurningSpeed * TankGame.DeltaTime);

            if (rightStick.Length() > 0)
            {
                var unprojectedPosition = GeometryUtils.ConvertWorldToScreen(new Vector3(0, 11, 0), World, View, Projection);
                Mouse.SetPosition((int)(unprojectedPosition.X + rightStick.X * 250), (int)(unprojectedPosition.Y - rightStick.Y * 250));
                //Mouse.SetPosition((int)(Input.CurrentMouseSnapshot.X + rightStick.X * TankGame.Instance.Settings.ControllerSensitivity), (int)(Input.CurrentMouseSnapshot.Y - rightStick.Y * TankGame.Instance.Settings.ControllerSensitivity));
            }

            Velocity = Vector2.UnitY.RotatedByRadians(TankRotation) * Properties.Speed;

            if (FireBullet.JustPressed)
                Shoot();
            if (PlaceMine.JustPressed)
                LayMine();
        }
        private void ControlHandle_Keybinding()
        {
            if (controlFirePath.JustPressed)
                _drawShotPath = !_drawShotPath;
            if (controlMine.JustPressed)
                LayMine();

            IsTurning = false;

            float treadPlaceTimer = (int)Math.Round(14 / Velocity.Length()) != 0 ? (int)Math.Round(14 / Velocity.Length()) : 1;

            var rotationMet = TankRotation > TargetTankRotation - Properties.MaximalTurn && TankRotation < TargetTankRotation + Properties.MaximalTurn;

            var preterbedVelocity = Vector2.Zero;

            TankRotation %= MathHelper.Tau;

            if (!rotationMet)
            {
                Properties.Speed -= Properties.Deceleration;
                if (Properties.Speed < 0)
                    Properties.Speed = 0;
                // treadPlaceTimer += MaxSpeed / 5;
                if (TankGame.RunTime % treadPlaceTimer <= TankGame.DeltaTime)
                    LayFootprint(Properties.TrackType == TrackID.Thick);
                IsTurning = true;
            }
            else
            {
                Properties.Speed += Properties.Acceleration;
                if (Properties.Speed > Properties.MaxSpeed)
                    Properties.Speed = Properties.MaxSpeed;
            }


            if (controlDown.IsPressed)
            {
                playerControl_isBindPressed = true;
                preterbedVelocity.Y = 1;
            }
            if (controlUp.IsPressed)
            {
                playerControl_isBindPressed = true;
                preterbedVelocity.Y = -1;
            }
            if (controlLeft.IsPressed)
            {
                playerControl_isBindPressed = true;
                preterbedVelocity.X = -1;
            }
            if (controlRight.IsPressed)
            {
                playerControl_isBindPressed = true;
                preterbedVelocity.X = 1;
            }

            if (Difficulties.Types["ThirdPerson"])
                preterbedVelocity = preterbedVelocity.RotatedByRadians(-TurretRotation + MathHelper.Pi);

            var norm = Vector2.Normalize(preterbedVelocity);

            TargetTankRotation = norm.ToRotation() - MathHelper.PiOver2;

            TankRotation = GameUtils.RoughStep(TankRotation, TargetTankRotation, Properties.TurningSpeed * TankGame.DeltaTime);

            Velocity = Vector2.UnitY.RotatedByRadians(TankRotation) * Properties.Speed;
        }
        public override void Destroy(ITankHurtContext context)
        {
            if (context.IsPlayer) {
                // this probably makes sense
                TankGame.GameData.Suicides++;
                PlayerStatistics.SuicidesThisCampaign++;
                // check if player id matches client id, if so, increment that player's kill count, then sync to the server
                // TODO: convert TankHurtContext into a struct and use it here
                // Will be used to track the reason of death and who caused the death, if any tank owns a shell or mine
                //
                // if (context.PlayerId == Client.PlayerId)
                // {
                //    PlayerTank.KillCount++;
                //    Client.Send(new TankKillCountUpdateMessage(PlayerTank.KillCount)); 
                //    not a bad idea actually
            }
            TankGame.GameData.Deaths++;

            GameHandler.AllPlayerTanks[PlayerId] = null;
            GameHandler.AllTanks[WorldId] = null;
            base.Destroy(context);
        }

        public void UpdatePlayerMovement()
        {
            if (!GameProperties.InMission)
                return;
            //if (!controlDown.IsPressed && !controlUp.IsPressed && leftStick.Y == 0)
                //Velocity.Y = 0;
            //if (!controlLeft.IsPressed && !controlRight.IsPressed && leftStick.X == 0)
                //Velocity.X = 0;

            if (Velocity.Length() > 0 && playerControl_isBindPressed)
            {
                var treadPlaceTimer = (int)Math.Round(14 / Velocity.Length()) != 0 ? (int)Math.Round(14 / Velocity.Length()) : 1;
                if (TankGame.RunTime % treadPlaceTimer <= TankGame.DeltaTime)
                    LayFootprint(Properties.TrackType == TrackID.Thick);
                if (!Properties.IsSilent)
                {
                    if (TankGame.RunTime % MathHelper.Clamp(treadPlaceTimer / 2, 4, 6) <= TankGame.DeltaTime)
                    {
                        var treadPlace = $"Assets/sounds/tnk_tread_place_{GameHandler.GameRand.Next(1, 5)}";
                        var sfx = SoundPlayer.PlaySoundInstance(treadPlace, SoundContext.Effect, 0.2f);
                        sfx.Instance.Pitch = Properties.TreadPitch;
                    }
                }
            }
        }

        private void DrawShootPath()
        {
            const int MAX_PATH_UNITS = 10000;

            var whitePixel = GameResources.GetGameResource<Texture2D>("Assets/textures/WhitePixel");
            var pathPos = Position + new Vector2(0, 18).RotatedByRadians(-TurretRotation);
            var pathDir = Vector2.UnitY.RotatedByRadians(TurretRotation - MathHelper.Pi);
            pathDir.Y *= -1;
            pathDir *= Properties.ShellSpeed;

            var pathRicochetCount = 0;


            for (int i = 0; i < MAX_PATH_UNITS; i++)
            {
                var dummyPos = Vector2.Zero;

                if (pathPos.X < MapRenderer.MIN_X || pathPos.X > MapRenderer.MAX_X)
                {
                    pathRicochetCount++;
                    pathDir.X *= -1;
                }
                if (pathPos.Y < MapRenderer.MIN_Y || pathPos.Y > MapRenderer.MAX_Y)
                {
                    pathRicochetCount++;
                    pathDir.Y *= -1;
                }

                var pathHitbox = new Rectangle((int)pathPos.X - 3, (int)pathPos.Y - 3, 6, 6);

                // Why is velocity passed by reference here lol
                Collision.HandleCollisionSimple_ForBlocks(pathHitbox, pathDir, ref dummyPos, out var dir, out var block, out bool corner, false, (c) => c.IsSolid);

                if (corner)
                    return;
                if (block != null)
                {
                    if (block.AllowShotPathBounce)
                    {
                        switch (dir)
                        {
                            case CollisionDirection.Up:
                            case CollisionDirection.Down:
                                pathDir.Y *= -1;
                                pathRicochetCount += block.PathBounceCount;
                                break;
                            case CollisionDirection.Left:
                            case CollisionDirection.Right:
                                pathDir.X *= -1;
                                pathRicochetCount += block.PathBounceCount;
                                break;
                        }
                    }
                }

                if (pathRicochetCount > Properties.RicochetCount)
                    return;

                if (GameHandler.AllTanks.Any(tnk => tnk is not null && tnk.CollisionCircle.Intersects(new Internals.Common.Framework.Circle() { Center = pathPos, Radius = 4 })))
                    return;

                pathPos += pathDir;
                // tainicom.Aether.Physics2D.Collision.
                var pathPosScreen = GeometryUtils.ConvertWorldToScreen(Vector3.Zero, Matrix.CreateTranslation(pathPos.X, 11, pathPos.Y), TankGame.GameView, TankGame.GameProjection);
                var off = (float)Math.Sin(i * Math.PI / 5 - TankGame.UpdateCount * 0.3f);
                TankGame.SpriteRenderer.Draw(whitePixel, pathPosScreen, null, (Color.White.ToVector3() * off).ToColor(), 0, whitePixel.Size() / 2, 2 + off, default, default);
            }
        }

        public override void Render()
        {
            DrawExtras();
            if (Properties.Invisible && GameProperties.InMission)
                return;
            for (int i = 0; i < (Lighting.AccurateShadows ? 2 : 1); i++)
            {
                foreach (ModelMesh mesh in Model.Meshes)
                {
                    foreach (BasicEffect effect in mesh.Effects)
                    {
                        effect.World = i == 0 ? boneTransforms[mesh.ParentBone.Index] : boneTransforms[mesh.ParentBone.Index] * Matrix.CreateShadow(Lighting.AccurateLightingDirection, new(Vector3.UnitY, 0)) * Matrix.CreateTranslation(0, 0.2f, 0);
                        effect.View = View;
                        effect.Projection = Projection;
                        effect.TextureEnabled = true;

                        if (!Properties.HasTurret)
                            if (mesh.Name == "Cannon")
                                return;

                        //effect.SpecularColor = Color.White.ToVector3();
                        //effect.SpecularPower = 10;

                        if (mesh.Name != "Shadow")
                        {
                            effect.Alpha = 1f;
                            effect.Texture = _tankTexture;

                            if (IsHoveredByMouse)
                                effect.EmissiveColor = Color.White.ToVector3();
                            else
                                effect.EmissiveColor = Color.Black.ToVector3();

                            /*var ex = new Color[1024];

                            Array.Fill(ex, Team != Team.NoTeam ? (Color)typeof(Color).GetProperty(Team.ToString(), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).GetValue(null) : default);

                            if (Team != Team.NoTeam)
                            {
                                effect.Texture.SetData(0, new Rectangle(0, 0, 32, 9), ex, 0, 288);
                                effect.Texture.SetData(0, new Rectangle(0, 23, 32, 9), ex, 0, 288);
                            }*/
                            mesh.Draw();
                        }

                        else
                        {
                            if (!Lighting.AccurateShadows)
                            {
                                if (IsIngame)
                                {
                                    effect.Alpha = 0.5f;
                                    effect.Texture = _shadowTexture;
                                    mesh.Draw();
                                }
                            }
                        }

                        effect.SetDefaultGameLighting_IngameEntities(specular: _isPlayerModel, ambientMultiplier: _isPlayerModel ? 2f : 0.9f);
                    }
                }
            }
            base.Render();
        }

        private void DrawExtras()
        {
            if (Dead)
                return;

            if (GameProperties.ShouldMissionsProgress && !GameProperties.InMission && IsIngame && !IntermissionSystem.IsAwaitingNewMission)
            {
                var tex1 = GameResources.GetGameResource<Texture2D>("Assets/textures/ui/chevron_border");
                var tex2 = GameResources.GetGameResource<Texture2D>("Assets/textures/ui/chevron_inside");

                var pos = GeometryUtils.ConvertWorldToScreen(Vector3.Zero, World, View, Projection) - new Vector2(0, 125).ToResolution();

                var playerColor = PlayerType == PlayerID.Blue ? Color.DeepSkyBlue : Color.Red;

                string pText = $"P{PlayerId + 1}"; // heeheeheeha

                float rotation = 0f;

                bool flip = false;

                if (pos.Y <= 150)
                {
                    flip = true;
                    pos.Y += 225;
                    rotation = MathHelper.Pi;
                }

                TankGame.SpriteRenderer.Draw(tex1, pos, null, Color.White, rotation, tex1.Size() / 2, 0.5f.ToResolution(), default, default);
                TankGame.SpriteRenderer.Draw(tex2, pos, null, playerColor, rotation, tex2.Size() / 2, 0.5f.ToResolution(), default, default);

                SpriteFontUtils.DrawBorderedText(TankGame.SpriteRenderer, TankGame.TextFontLarge, pText, new(pos.X, pos.Y + (flip ? 100 : -125).ToResolutionY()), playerColor, Color.White, Vector2.One.ToResolution(), 0f, 2f);
            }

            if (DebugUtils.DebugLevel == 1 || _drawShotPath)
                DrawShootPath();

            if (Properties.Invisible && GameProperties.InMission)
                return;

            Properties.Armor?.Render();
        }

        public override string ToString()
            => $"pos: {Position} | vel: {Velocity} | dead: {Dead} | rotation: {TankRotation} | OwnedBullets: {OwnedShellCount}";
    }
}