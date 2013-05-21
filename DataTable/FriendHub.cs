﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;

namespace DataTable
{
    public class FriendHub : Hub
    {
        private static DataContext _db = new DataContext();
        private static ConcurrentDictionary<string, int> _locks = new ConcurrentDictionary<string, int>();

        public override Task OnConnected()
        {
            var query = from f in _db.Friends
                        orderby f.Name
                        select f;

            Task firstTask = Clients.Caller.all(query);
            return firstTask.ContinueWith(task => Clients.Caller.allLocks(_locks.Values));
        }

        public override Task OnReconnected()
        {
            // Refresh as other users could update data while we were offline
            return OnConnected();
        }

        public override Task OnDisconnected()
        {
            int removed;
            _locks.TryRemove(Context.ConnectionId, out removed);
            return Clients.All.allLocks(_locks.Values);
        }

        public void Add(Friend value)
        {
            var added = _db.Friends.Add(value);
            _db.SaveChanges();

            Clients.All.add(added);
        }

        public void Delete(Friend value)
        {
            var entity = _db.Friends.First<Friend>(f => f.Id == value.Id);
            var removed = _db.Friends.Remove(entity);
            _db.SaveChanges();

            Clients.All.delete(removed);
        }        

        public void Update(Friend value)
        {
            var updated = _db.Friends.First<Friend>(f => f.Id == value.Id);
            updated.Name = value.Name;
            _db.SaveChanges();

            Clients.All.update(updated);

            int removed;
            _locks.TryRemove(Context.ConnectionId, out removed);
            Clients.All.allLocks(_locks.Values);
        }

        public void AllLocks()
        {
            Clients.Caller.allLocks(_locks.Values);
        }

        public void TakeLock(Friend value)
        {
            _locks.AddOrUpdate(Context.ConnectionId, value.Id, (key, oldValue) => value.Id);
            Clients.All.allLocks(_locks.Values);
        }
    }
}