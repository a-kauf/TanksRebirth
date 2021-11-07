﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using WiiPlayTanksRemake.Internals.Core;

namespace WiiPlayTanksRemake.Internals.UI
{
    public abstract class UIElement
    {
        public delegate void MouseEvent(UIElement affectedElement);

        private Vector2 InternalPosition;

        private Vector2 InternalSize;

        public static List<UIElement> AllUIElements { get; internal set; } = new();

        public UIElement Parent { get; private set; }

        protected IList<UIElement> Children { get; set; } = new List<UIElement>();

        public Rectangle Hitbox => new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y);

        public Vector2 Position { get; private set; }

        public Vector2 Size { get; private set; }

        public Vector2 ScaleOrigin = new Vector2(0.5f);

        public bool Visible = true;

        public bool MouseHovering;

        public bool Initialized;

        public float Rotation { get; set; } = 0;

        public event MouseEvent OnMouseClick;

        public event MouseEvent OnMouseRightClick;

        public event MouseEvent OnMouseMiddleClick;

        public event MouseEvent OnMouseOver;

        public event MouseEvent OnMouseLeave;

        internal UIElement() {
            AllUIElements.Add(this);
        }

        public void SetDimensions(int x, int y, int width, int height) {
            InternalPosition = new Vector2(x, y);
            InternalSize = new Vector2(width, height);
            Recalculate();
        }

        public void Recalculate() {
            Position = InternalPosition;
            Size = InternalSize;
        }

        public virtual void Draw(SpriteBatch spriteBatch) {
            if (!Visible)
                return;

            DrawSelf(spriteBatch);
            DrawChildren(spriteBatch);
        }

        public void Initialize() {
            if (Initialized)
                return;

            OnInitialize();
            Initialized = true;
        }

        public virtual void OnInitialize() {
            foreach (UIElement child in Children) {
                child.Initialize();
            }
        }

        public virtual void DrawSelf(SpriteBatch spriteBatch) { }

        public virtual void DrawChildren(SpriteBatch spriteBatch) {
            foreach (UIElement child in Children) {
                child.Draw(spriteBatch);
            }
        }

        public virtual void Append(UIElement element) {
            element.Remove();
            element.Parent = this;
            Children.Add(element);
        }

        public virtual void Remove() {
            Parent?.Children.Remove(this);
            Parent = null;
        }

        public virtual void Remove(UIElement child) {
            Children.Remove(child);
            child.Parent = null;
        }

        public virtual void MouseClick() {
            OnMouseClick?.Invoke(this);
        }

        public virtual void MouseRightClick() {
            OnMouseRightClick?.Invoke(this);
        }

        public virtual void MouseMiddleClick() {
            OnMouseMiddleClick?.Invoke(this);
        }

        public virtual void MouseOver() {
            OnMouseOver?.Invoke(this);
            MouseHovering = true;
        }

        public virtual void MouseLeave() {
            OnMouseLeave?.Invoke(this);
            MouseHovering = false;
        }
    }
}