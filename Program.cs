using System;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using isci.Allgemein;
using isci.Daten;
using isci.Beschreibung;

namespace isci.msb
{
    public class Konfiguration : Parameter
    {
        public string uuid, name, description, token, target_interface;
        public Konfiguration(string datei) : base(datei) {

        }
    }

    class Program
    {
        static Dictionary<string, object> puffer = new Dictionary<string, object>();

        public static void msbCallback_Connected(object sender, System.EventArgs e)
        {
            msbClient.RegisterAsync(msbApplication);
        }
        public static void msbCallback_Disconnected(object sender, System.EventArgs e)
        {
            msbActive = false;
        }
        public static void msbCallback_Registered(object sender, System.EventArgs e)
        {
            msbActive = true;
        }
        public static void msbCallback_FunctionCallMethod([Fraunhofer.IPA.MSB.Client.API.Attributes.MsbFunctionParameter(Name = "val")]System.Collections.Generic.Dictionary<string, object> val, Fraunhofer.IPA.MSB.Client.API.Model.FunctionCallInfo info)
        {
            foreach (var obj in val)
            {
                if (puffer.ContainsKey(obj.Key))
                {
                    puffer[obj.Key] = obj.Value;
                } else {
                    puffer.Add(obj.Key, obj.Value);
                }
            }
        }

        public static void ApplikationBauen()
        {
            var Events = new System.Collections.Generic.List<isci.Anwendungen.Ereignis>();
            var Funktionen = new System.Collections.Generic.List<isci.Anwendungen.Funktion>();

            var dateien = System.IO.Directory.GetFiles(konfiguration.OrdnerEreignismodelle, "*.json");

            foreach (var datei_ in dateien)
            {
                var datei = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<isci.Anwendungen.Ereignis>>(System.IO.File.ReadAllText(datei_));
                if (datei != null)
                {
                    Events.AddRange(datei);
                }
            }

            dateien = System.IO.Directory.GetFiles(konfiguration.OrdnerFunktionsmodelle, "*.json");

            foreach (var datei_ in dateien)
            {
                var datei = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<isci.Anwendungen.Funktion>>(System.IO.File.ReadAllText(datei_));
                if (datei != null)
                {
                    Funktionen.AddRange(datei);
                }
            }

            foreach (var f in Funktionen)
            {
                var df = new Fraunhofer.IPA.MSB.Client.Websocket.Model.DataFormat();
                var mInfo = typeof(Program).GetMethod("msbCallback_FunctionCallMethod");
                var f_ = new Fraunhofer.IPA.MSB.Client.API.Model.Function(f.Identifikation, f.Name, f.Beschreibung, null, mInfo, null);
                msbApplication.AddFunction(f_);
            }

            foreach (var e in Events)
            {
                var df = new Fraunhofer.IPA.MSB.Client.Websocket.Model.DataFormat();
                var e_ = new Fraunhofer.IPA.MSB.Client.API.Model.Event(e.Identifikation, e.Name, e.Beschreibung, df);
                msbApplication.AddEvent(e_);

                AuslöserEreignis = new System.Collections.Generic.List<KeyValuePair<isci.Daten.Dateneintrag, isci.Anwendungen.Ereignis>>();
                EreignisEvent = new System.Collections.Generic.Dictionary<isci.Anwendungen.Ereignis, Fraunhofer.IPA.MSB.Client.API.Model.Event>();

                AuslöserEreignis.Add(new KeyValuePair<Dateneintrag, Anwendungen.Ereignis>(structure.dateneinträge[e.Ausloeser], e));
                EreignisEvent.Add(e, e_);
            }
        }

        static System.Collections.Generic.List<KeyValuePair<isci.Daten.Dateneintrag, isci.Anwendungen.Ereignis>> AuslöserEreignis;
        static System.Collections.Generic.Dictionary<isci.Anwendungen.Ereignis, Fraunhofer.IPA.MSB.Client.API.Model.Event> EreignisEvent;       
        static Fraunhofer.IPA.MSB.Client.Websocket.MsbClient msbClient;
        static Fraunhofer.IPA.MSB.Client.API.Model.Application msbApplication;
        static bool msbActive = false;
        static Datenstruktur structure;
        static Konfiguration konfiguration;
        static void Main(string[] args)
        {
            konfiguration = new Konfiguration("konfiguration.json");

            msbClient = new MsbClientFP(konfiguration.target_interface);
            msbClient.Connected += msbCallback_Connected;
            msbClient.Disconnected += msbCallback_Disconnected;
            msbClient.Registered += msbCallback_Registered;
            msbClient.AutoReconnect = true;
            msbClient.AutoReconnectIntervalInMilliseconds = 10000;
            
            structure = new Datenstruktur(konfiguration.OrdnerDatenstruktur);
            structure.DatenmodelleEinhängenAusOrdner(konfiguration.OrdnerDatenmodelle);
            
            msbApplication = new Fraunhofer.IPA.MSB.Client.API.Model.Application(konfiguration.uuid, konfiguration.name, konfiguration.description, konfiguration.token);
            ApplikationBauen();

            var beschreibung = new Modul(konfiguration.Identifikation, "isci.msb");
            beschreibung.Name = "Modul MSB " + konfiguration.Identifikation;
            beschreibung.Beschreibung = "Modul MSB";
            beschreibung.Speichern(konfiguration.OrdnerBeschreibungen + "/" + konfiguration.Identifikation + ".json");

            msbClient.ConnectAsync();
            
            structure.Start();

            var Zustand = new dtZustand(konfiguration.OrdnerDatenstruktur);
            Zustand.Start();
            
            while(true)
            {
                Zustand.Lesen();

                var erfüllteTransitionen = konfiguration.Ausführungstransitionen.Where(a => a.Eingangszustand == (System.Int32)Zustand.value);
                if (erfüllteTransitionen.Count<Ausführungstransition>() <= 0) continue;

                if (erfüllteTransitionen.ElementAt(0) == konfiguration.Ausführungstransitionen.ElementAt(0))
                {
                    foreach (var gespeichert in puffer)
                    {
                        var eintrag = structure.dateneinträge[gespeichert.Key];
                        var val = gespeichert.Value;
                        switch (eintrag.type)
                        {
                            case Datentypen.Bool: structure.dateneinträge[gespeichert.Key].value = (bool)val; break;
                            case Datentypen.Int8: structure.dateneinträge[gespeichert.Key].value = (SByte)val; break;
                            case Datentypen.Int16: structure.dateneinträge[gespeichert.Key].value = (Int16)val; break;
                            case Datentypen.Int32: structure.dateneinträge[gespeichert.Key].value = (Int32)val; break;
                            case Datentypen.UInt8: structure.dateneinträge[gespeichert.Key].value = (byte)val; break;
                            case Datentypen.UInt16: structure.dateneinträge[gespeichert.Key].value = (UInt16)val; break;
                            case Datentypen.UInt32: structure.dateneinträge[gespeichert.Key].value = (UInt32)val; break;
                            case Datentypen.String: structure.dateneinträge[gespeichert.Key].value = (String)val; break;
                            case Datentypen.Float: structure.dateneinträge[gespeichert.Key].value = (float)val; break;
                            case Datentypen.Double: structure.dateneinträge[gespeichert.Key].value = (Double)val; break;
                            default:continue;
                        }
                        eintrag.Schreiben();
                    }
                    puffer.Clear();

                } else if (erfüllteTransitionen.ElementAt(0) == konfiguration.Ausführungstransitionen.ElementAt(1))
                {
                    structure.Lesen();

                    foreach (var ausloeser in AuslöserEreignis)
                    {
                        if (ausloeser.Key.aenderung)
                        {
                            var eventData = new Fraunhofer.IPA.MSB.Client.API.Model.EventData(EreignisEvent[ausloeser.Value]);
                            var data = new Dictionary<string, object>();
                            foreach (var element in ausloeser.Value.Elemente)
                            {
                                data.Add(element, structure.dateneinträge[element].value);
                            }
                            eventData.Value = data;
                            msbClient.PublishAsync(msbApplication, eventData);
                        }
                    }

                    structure.AenderungenZuruecksetzen();
                }

                Zustand.value = erfüllteTransitionen.First<Ausführungstransition>().Ausgangszustand;
                Zustand.Schreiben();
            }
        }
    }
}