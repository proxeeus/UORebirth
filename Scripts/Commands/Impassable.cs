using Server.Commands;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Server.Scripts.Commands
{
    class Impassable
    {
        private static Queue m_ToDelete = new Queue();

        public static void Initialize()
        {
            CommandSystem.Register("Impassable", AccessLevel.Administrator, new CommandEventHandler(Impassable_OnCommand));
        }

        [Usage("Impassable")]
        [Description("Removes all Impassable objects from the map.")]
        private static void Impassable_OnCommand(CommandEventArgs e)
        {
            try
            {
                IPooledEnumerable eable = null;
                foreach (var witem in World.Items)
                {
                    if (witem.Value.ItemID == 8612 || witem.Value.ItemID == 8601)
                    {
                        eable = witem.Value.Map.GetItemsInRange(new Point3D(witem.Value.X, witem.Value.Y, witem.Value.Z), 0);

                        foreach (Item item in eable)
                        {
                            if (item is Item && (item.ItemID == 8612 || witem.Value.ItemID == 8601) && item.Z == witem.Value.Z)
                                m_ToDelete.Enqueue(item);
                        }
                    }
                }
                eable.Free();

                while (m_ToDelete.Count > 0)
                    ((Item)m_ToDelete.Dequeue()).Delete();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
