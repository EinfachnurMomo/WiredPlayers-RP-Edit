﻿using GTANetworkAPI;
using System.Collections.Generic;
using WiredPlayers.database;
using WiredPlayers.globals;
using WiredPlayers.model;
using System;

namespace WiredPlayers.house
{
    class House : Script
    {
        public static List<HouseModel> houseList;

        public House()
        {
            Event.OnClientEventTrigger += onClientEventHandler;
        }

        private void onClientEventHandler(Client player, string eventName, params object[] arguments)
        {
            if (eventName == "wardrobeClothesItemSelected")
            {
                int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                int clothesId = Int32.Parse(arguments[0].ToString());
                int type = Int32.Parse(arguments[1].ToString());
                int slot = Int32.Parse(arguments[2].ToString());

                // Quitamos la ropa que tenía puesta y ponemos la nueva
                foreach(ClothesModel clothes in Globals.clothesList)
                {
                    if(clothes.id == clothesId)
                    {
                        clothes.dressed = true;
                        if (clothes.type == 0)
                        {
                            NAPI.Player.SetPlayerClothes(player, clothes.slot, clothes.drawable, 0);
                        }
                        else
                        {
                            NAPI.Player.SetPlayerAccessory(player, clothes.slot, clothes.drawable, 0);
                        }

                        // Actualizamos la ropa en la base de datos
                        Database.updateClothes(clothes);
                    }
                    else if(clothes.id != clothesId && clothes.player == playerId && clothes.type == type && clothes.slot == slot && clothes.dressed)
                    {
                        clothes.dressed = false;

                        // Actualizamos la ropa en la base de datos
                        Database.updateClothes(clothes);
                    }
                }
            }
        }

        public void loadDatabaseHouses()
        {
            houseList = Database.loadAllHouses();
            foreach (HouseModel houseModel in houseList)
            {
                String houseLabelText = getHouseLabelText(houseModel);
                houseModel.houseLabel = NAPI.TextLabel.CreateTextLabel(houseLabelText, houseModel.position, 20.0f, 0.75f, 0, new Color(255, 255, 255), false, houseModel.dimension);
            }
        }

        public static HouseModel getHouseById(int id)
        {
            HouseModel house = null;
            foreach (HouseModel houseModel in houseList)
            {
                if(houseModel.id == id)
                {
                    house = houseModel;
                    break;
                }
            }
            return house;
        }

        public static HouseModel getClosestHouse(Client player, float distance = 1.5f)
        {
            HouseModel house = null;
            foreach(HouseModel houseModel in houseList)
            {
                if(player.Position.DistanceTo(houseModel.position) < distance)
                {
                    house = houseModel;
                    distance = player.Position.DistanceTo(houseModel.position);
                }
            }
            return house;
        }

        public static Vector3 getHouseExitPoint(String ipl)
        {
            Vector3 exit = null;
            foreach(HouseIplModel houseIpl in Constants.HOUSE_IPL_LIST)
            {
                if(houseIpl.ipl == ipl)
                {
                    exit = houseIpl.position;
                    break;
                }
            }
            return exit;
        }

        public static bool hasPlayerHouseKeys(Client player, HouseModel house)
        {
            return (player.Name == house.owner || NAPI.Data.GetEntityData(player, EntityData.PLAYER_RENT_HOUSE) == house.id);
        }

        public static String getHouseLabelText(HouseModel house)
        {
            String label = String.Empty;
            switch(house.status)
            {
                case Constants.HOUSE_STATE_NONE:
                    label = house.name + "\n" + "Estado: Ocupada";
                    break;
                case Constants.HOUSE_STATE_RENTABLE:
                    label = house.name + "\n" + "Estado: En alquiler\n" + house.rental + "$";
                    break;
                case Constants.HOUSE_STATE_BUYABLE:
                    label = house.name + "\n" + "Estado: En venta\n" + house.price + "$";
                    break;
            }
            return label;
        }

        public static void buyHouse(Client player, HouseModel house)
        {
            if(house.status == Constants.HOUSE_STATE_BUYABLE)
            {
                if(NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_BANK) >= house.price)
                {
                    int bank = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_BANK) - house.price;
                    String message = String.Format(Messages.INF_HOUSE_BUY, house.name, house.price);
                    String labelText = getHouseLabelText(house);
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);
                    NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_BANK, bank);
                    house.status = Constants.HOUSE_STATE_NONE;
                    NAPI.TextLabel.SetTextLabelText(house.houseLabel, getHouseLabelText(house));
                    house.owner = player.Name;
                    house.locked = true;
                    Database.updateHouse(house);
                }
                else
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_HOUSE_NOT_MONEY);
                }
            }
            else
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_HOUSE_NOT_BUYABLE);
            }
        }

        [Command("alquilable", Messages.GEN_RENTABLE_COMMAND)]
        public void alquilableCommand(Client player, int amount = 0)
        {
            if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED) == 0)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_IN_HOUSE);
            }
            else
            {
                int houseId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED);
                HouseModel house = getHouseById(houseId);
                if(house == null || house.owner != player.Name)
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_HOUSE_OWNER);
                }
                else if(amount > 0)
                {
                    house.rental = amount;
                    house.status = Constants.HOUSE_STATE_RENTABLE;
                    house.tenants = 2;
                    Database.updateHouse(house);

                    // Actualizamos el label de la casa
                    String labelText = getHouseLabelText(house);
                    NAPI.TextLabel.SetTextLabelText(house.houseLabel, labelText);

                    // Mandamos el mensaje al jugador
                    String message = String.Format(Messages.INF_HOUSE_STATE_RENT, amount);
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);
                }
                else if (house.status == Constants.HOUSE_STATE_RENTABLE)
                {
                    house.status = Constants.HOUSE_STATE_NONE;
                    Database.updateHouse(house);

                    // Actualizamos el label de la casa
                    String labelText = getHouseLabelText(house);
                    NAPI.TextLabel.SetTextLabelText(house.houseLabel, labelText);

                    // Mandamos el mensaje al jugador
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_HOUSE_RENT_CANCEL);

                    Database.kickTenantsOut(house.id);
                    house.tenants = 2;
                    Database.updateHouse(house);
                }
                else
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PRICE_POSITIVE);
                }
            }
        }

        [Command("alquilar")]
        public void alquilarCommand(Client player)
        {
            // Recorremos la lista de casas
            foreach (HouseModel house in houseList)
            {
                if (player.Position.DistanceTo(house.position) <= 1.5 && player.Dimension == house.dimension)
                {
                    if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_RENT_HOUSE) == 0)
                    {
                        if (house.status != Constants.HOUSE_STATE_RENTABLE)
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_HOUSE_NOT_RENTABLE);
                        }
                        else if (NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_MONEY) < house.rental)
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_RENT_MONEY);
                        }
                        else
                        {
                            int money = NAPI.Data.GetEntitySharedData(player, EntityData.PLAYER_MONEY) - house.rental;
                            String message = String.Format(Messages.INF_HOUSE_RENT, house.name, house.rental);
                            NAPI.Data.SetEntityData(player, EntityData.PLAYER_RENT_HOUSE, house.id);
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + message);
                            NAPI.Data.SetEntitySharedData(player, EntityData.PLAYER_MONEY, money);
                            house.tenants--;
                            if (house.tenants == 0)
                            {
                                house.status = Constants.HOUSE_STATE_NONE;
                                String labelText = getHouseLabelText(house);
                                NAPI.TextLabel.SetTextLabelText(house.houseLabel, labelText);
                            }

                            // Actualizamos los inquilinos
                            Database.updateHouse(house);
                        }
                        break;
                    }
                    else if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_RENT_HOUSE) == house.id)
                    {
                        // desalquilar.
                        NAPI.Data.SetEntityData(player, EntityData.PLAYER_RENT_HOUSE, 0);
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + String.Format(Messages.INF_HOUSE_RENT_STOP, house.name));
                        house.tenants++;
                        Database.updateHouse(house);
                    }
                    else
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_HOUSE_RENTED);
                    }
                }
            }         
        }

        [Command("armario")]
        public void armarioCommand(Client player)
        {
            int houseId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_HOUSE_ENTERED);
            if(houseId > 0)
            {
                HouseModel house = getHouseById(houseId);
                if (hasPlayerHouseKeys(player, house) == true)
                {
                    int playerId = NAPI.Data.GetEntityData(player, EntityData.PLAYER_SQL_ID);
                    List<ClothesModel> clothesList = Globals.getPlayerClothes(playerId);
                    List<String> clothesNames = Globals.getClothesNames(clothesList);
                    if(clothesList.Count > 0)
                    {
                        NAPI.ClientEvent.TriggerClientEvent(player, "showPlayerWardrobe", NAPI.Util.ToJson(clothesList), NAPI.Util.ToJson(clothesNames));
                    }
                    else
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NO_CLOTHES_IN_WARDROBE);
                    }
                }
                else
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_HOUSE_OWNER);
                }
            }
            else
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_IN_HOUSE);
            }
        }
    }
}