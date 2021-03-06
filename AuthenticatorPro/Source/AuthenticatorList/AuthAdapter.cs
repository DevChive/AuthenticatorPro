﻿using System;
using System.Collections.Generic;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using AuthenticatorPro.Data;
using OtpSharp;
using Object = Java.Lang.Object;

namespace AuthenticatorPro.AuthenticatorList
{
    internal sealed class AuthAdapter : RecyclerView.Adapter, IAuthAdapterMovement
    {
        private readonly bool _isDark;
        private readonly AuthSource _source;

        public AuthAdapter(AuthSource authSource, bool isDark)
        {
            _isDark = isDark;
            _source = authSource;
        }

        public override int ItemCount => _source.Count();

        public void OnViewMoved(int oldPosition, int newPosition)
        {
            _source.Move(oldPosition, newPosition);
            NotifyItemMoved(oldPosition, newPosition);
        }

        public event EventHandler<int> ItemClick;
        public event EventHandler<int> ItemOptionsClick;

        public override void OnBindViewHolder(RecyclerView.ViewHolder viewHolder, int position)
        {
            var auth = _source.Get(position);
            var holder = (AuthHolder) viewHolder;

            holder.Issuer.Text = auth.Issuer;
            holder.Username.Text = auth.Username;

            holder.Username.Visibility = auth.Username == ""
                ? ViewStates.Gone
                : ViewStates.Visible;

            var codePadded = auth.Code;
            var spacesInserted = 0;
            var length = codePadded.Length;

            for(var i = 0; i < length; ++i)
                if(i % 3 == 0 && i > 0)
                {
                    codePadded = codePadded.Insert(i + spacesInserted, " ");
                    spacesInserted++;
                }

            holder.Icon.SetImageResource(Icons.GetService(auth.Icon, _isDark));

            if(auth.Type == OtpType.Totp)
                TotpViewBind(holder, auth);

            else if(auth.Type == OtpType.Hotp)
                HotpViewBind(holder, auth);

            holder.Code.Text = codePadded;
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder viewHolder, int position, IList<Object> payloads)
        {
            if(payloads == null || payloads.Count == 0)
                OnBindViewHolder(viewHolder, position);
            else
            {
                var auth = _source.Authenticators[position];
                var holder = (AuthHolder) viewHolder;

                if(auth.Type == OtpType.Totp)
                    holder.ProgressBar.Progress = GetTotpRemainingProgress(auth);
                else if(auth.Type == OtpType.Hotp)
                    holder.RefreshButton.Visibility = ViewStates.Visible;
            }
        }

        private static int GetTotpRemainingProgress(IAuthenticatorInfo auth)
        {
            var secondsRemaining = (auth.TimeRenew - DateTime.Now).TotalSeconds;
            return (int) Math.Ceiling(100d * secondsRemaining / auth.Period);
        }

        private static void TotpViewBind(AuthHolder holder, Authenticator auth)
        {
            holder.RefreshButton.Visibility = ViewStates.Gone;
            holder.ProgressBar.Visibility = ViewStates.Visible;
            holder.Counter.Visibility = ViewStates.Invisible;
            holder.ProgressBar.Progress = GetTotpRemainingProgress(auth);
        }

        private static void HotpViewBind(AuthHolder holder, Authenticator auth)
        {
            holder.RefreshButton.Visibility = auth.TimeRenew < DateTime.Now
                ? ViewStates.Visible
                : ViewStates.Gone;

            holder.ProgressBar.Visibility = ViewStates.Invisible;
            holder.Counter.Visibility = ViewStates.Visible;
            holder.Counter.Text = auth.Counter.ToString();
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var itemView = LayoutInflater.From(parent.Context).Inflate(
                Resource.Layout.authListItem, parent, false);

            var holder = new AuthHolder(itemView, OnItemClick, OnItemOptionsClick, OnRefreshClick);

            return holder;
        }

        private void OnItemClick(int position)
        {
            ItemClick?.Invoke(this, position);
        }

        private void OnItemOptionsClick(int position)
        {
            ItemOptionsClick?.Invoke(this, position);
        }

        private async void OnRefreshClick(int position)
        {
            await _source.IncrementHotp(position);
            NotifyItemChanged(position);
        }

        public override long GetItemId(int position)
        {
            return _source.Authenticators[position].GetHashCode();
        }
    }
}