//
//WebServiceBackEnd.cs
//
// Copyright (C) 2005 Novell, Inc.
//
// Authors:
//   Vijay K. Nanjundaswamy (knvijay@novell.com)
//

//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//
using System;
using System.IO;
using System.Net;
using System.Collections;
using System.Threading;
using System.Diagnostics;

using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

using Beagle.Util;
using Beagle.Daemon;

using Mono.ASPNET;

namespace Beagle.WebService {

	public class WebServiceBackEnd: MarshalByRefObject   {

		public static string hostname = "localhost";		
		public static string DEFAULT_XSP_ROOT = Path.Combine (ExternalStringsHack.PkgDataDir, "xsp");
		public static string DEFAULT_XSP_PORT = "8888";
		public static string web_rootDir = DEFAULT_XSP_ROOT;
		public static string web_port = DEFAULT_XSP_PORT;		
		public static bool web_start = true;
		public static bool web_global = false;

		static Mono.ASPNET.ApplicationServer appServer = null;
		//Both "/" and "/beagle" aliased to DEFAULT_XSP_ROOT only for BeagleXSP server
		static string DEFAULT_APP_MAPPINGS = "/:" + DEFAULT_XSP_ROOT + ",/beagle:" + DEFAULT_XSP_ROOT;
		static string[] xsp_param = {"--port", 	DEFAULT_XSP_PORT,
					     			 "--root", 	DEFAULT_XSP_ROOT, 
					     			 "--applications", DEFAULT_APP_MAPPINGS, 
					     			 "--nonstop"};
					     			 
		//static Logger log = Logger.Get ("WebServiceBackEnd");		
		static string 		BeagleHttpUriBase;
		static string[] 	reserved_suffixes;

		public static ExternalAccessFilter AccessFilter;
		
		public static void Start()
		{			
			if (! web_start) {
				Logger.Log.Warn("Beagle running with WebServices DISABLED\n");
				return;
			}
				
			try {
				IPHostEntry hostInfo = Dns.GetHostByName(Dns.GetHostName());
				//Fully qualified DNS name of host:
				hostname = hostInfo.HostName;
				Logger.Log.Info("This Computer Hostname: " + hostname);
			}
			catch (Exception ex) 
			{
				Logger.Log.Error("Caught exception {0} in Dns.GetHostName: ", ex.Message);
				Logger.Log.Error("Resetting hostname to \"localhost\"");
				hostname = "localhost";
			}
			
			if (! web_global)
				web_global = Conf.WebServices.AllowGlobalAccess;
			
			//start web-access server first
			Logger.Log.Debug ("Starting WebBackEnd");
			WebBackEnd.init ();

			//Next start web-service server
			Logger.Log.Info ("Starting WebServiceBackEnd");
			WebServiceBackEnd.init ();

			Logger.Log.Debug ("Global WebServicesAccess {0}", web_global ? "Enabled" : "Disabled");

			xsp_param[1] = web_port;
			xsp_param[3] = web_rootDir;
			
			//Check if web_rootDir_changed:
			if (String.Compare(web_rootDir, DEFAULT_XSP_ROOT, true) != 0)
				//Assuming "/beagle" exists as an explicit sub-folder under user specified xsp root directory:
				xsp_param[5] = "/:" + web_rootDir + ",/beagle:" + web_rootDir + "/beagle";
			
			try {
					// Mapping /beagle/local to ExternalStringsHack.Prefix	
				if (Directory.Exists(ExternalStringsHack.Prefix))
				xsp_param[5] += ",/beagle/local:" + ExternalStringsHack.Prefix;
							
					//Mapping /beagle/gnome to ExternalStringsHack.GnomePrefix
				if (Directory.Exists(ExternalStringsHack.GnomePrefix))
				xsp_param[5] += ",/beagle/gnome:" + ExternalStringsHack.GnomePrefix;
												
					//Mapping /beagle/kde3 to ExternalStringsHack.KdePrefix
				if (Directory.Exists(ExternalStringsHack.KdePrefix))
				xsp_param[5] += ",/beagle/kde3:" + ExternalStringsHack.KdePrefix;

				string imgDir = PathFinder.StorageDir + "/img";
				if (!Directory.Exists(imgDir))
				{
					Process pr = new Process ();
					pr.StartInfo.UseShellExecute = true; 
					pr.StartInfo.FileName = "mkdir"; 
					pr.StartInfo.Arguments = imgDir;

					try {		
						pr.Start ();
						pr.WaitForExit(); 	  				
	  					pr.Close();
	  					pr.Dispose();				
					} 
					catch (Exception e) { 
						Logger.Log.Warn("Error creating ~/.beagle/img folder"); 
					} 				
				}
				xsp_param[5] += ",/beagle/img:" + imgDir;
				
				//if (!hostname.Equals("localhost")) {

					reserved_suffixes = new string[] {"beagle", "local", "gnome", "kde3"};
					BeagleHttpUriBase = "http://" + hostname + ":" + xsp_param[1] + "/beagle/";
				
					AccessFilter = new ExternalAccessFilter(BeagleHttpUriBase, reserved_suffixes);
				
					ArrayList matchers = AccessFilter.Matchers;				
					foreach (SimpleMatcher sm in matchers) 					
						xsp_param[5] += ",/beagle/" + sm.Rewrite +":" + sm.Match;					
							
					AccessFilter.Initialize();
				//}
			}
			catch (Exception e)
			{
				xsp_param[5] = DEFAULT_APP_MAPPINGS;
			}					
				
			Logger.Log.Debug ("Starting Internal Web Server");

			int retVal = 0;
			try {
				//Start beagled internal web server (BeagleXsp)
				retVal = Mono.ASPNET.Server.initXSP(xsp_param, out appServer);
			}
			catch (ArgumentException e) {
				//Retry with default application mappings:
				xsp_param[5] = DEFAULT_APP_MAPPINGS;
				retVal = Mono.ASPNET.Server.initXSP(xsp_param, out appServer);		
			}
			catch (System.Net.Sockets.SocketException) {
					Logger.Log.Error ("Error starting Internal Web Server (retVal={0})", retVal);
					Logger.Log.Error("There is probably another beagled instance running.  "
							  + "Use --replace to replace the running service");			
			}

			if (retVal == 0) 
				Logger.Log.Debug("BeagleXSP Applications list: " + xsp_param[5]);
		}
		
		public static void Stop() 
		{
			Logger.Log.Info ("Stopping WebServiceBackEnd");
			if (appServer != null) {
			    appServer.Stop(); 
				appServer = null;
			}
			
			Process pr = new Process ();
			pr.StartInfo.UseShellExecute = true; 
			pr.StartInfo.FileName = "rm"; 
			pr.StartInfo.Arguments = " -rf  " + PathFinder.StorageDir + "/img/*";

			try {		
				pr.Start ();
				pr.WaitForExit(); 	  				
	  			pr.Close();
	  			pr.Dispose();					
			} 
			catch (Exception e) { } 
									
			WebBackEnd.cleanup();
			instance = null;
		}
		
/////////////////////////////////////////////////////////////////////////////////////////

		private void WebServicesConfigurationChanged (Conf.Section section)
		{			
			Logger.Log.Info("WebServicesConfigurationChanged EventHandler invoked");
			if (! (section is Conf.WebServicesConfig))
				return;
			
			Conf.WebServicesConfig wsc = (Conf.WebServicesConfig) section;
			
			if (web_global != wsc.AllowGlobalAccess) {
				// Update AllowGlobalAccess configuration:
				web_global = wsc.AllowGlobalAccess;
				Logger.Log.Info("WebServicesBackEnd: Global WebServicesAccess {0}", web_global ? "Enabled" : "Disabled");
			}
			
			if (wsc.PublicFolders.Count == 0)
				return;
				
			Logger.Log.Warn("WebServicesBackEnd: Changes in PublicFolders configuration doesn't take effect until Beagle daemon is restarted!");
				
/*			Note: ExternalAccessFilter Matchers can be updated, 
				  but app mapping changes in BeagleXsp Server require it to be restarted !
			
			ArrayList newList = new ArrayList();
			foreach (string pf in wsc.PublicFolders) {
					if (pf == null || pf == "")
						continue;					
	
					newList.Add (new NetBeagleHandler (host, port, this));								
			}	
			
			if (usingPublicFoldersDotCfgFile) {
				usingPublicFoldersDotCfgFile = false;
				log.Warn("NetBeagleQueryable: Duplicate configuration of PublicFolders in '~/.beagle/publicfolders.cfg' and '~/.beagle/config/webservices.xml' !");
				log.Info("NetBeagleQueryable: Remove '~/.beagle/publicfolders.cfg' file. Use 'beagle-config' instead to setup public folders.");		
				log.Info("NetBeagleQueryable: Replacing PublicFoldersList with new list from \"webservices.xml\" having {0} node(s)", newList.Count);
			}			
			
			AccessFilter.ReplaceAccessFilter(newList);  			
*/					 
		}
		
/////////////////////////////////////////////////////////////////////////////////////////		
			
		//KNV: If needed, we can convert this to a Singleton, adding a 
		//	   static Factory method to get the singleton instance reference,
		//	   so that front-end code always gets hold of same instance.
		
		static WebServiceBackEnd instance = null;		

		private Hashtable resultTable;
		private Hashtable sessionTable;
		
		public WebServiceBackEnd() {

			 resultTable 		= Hashtable.Synchronized(new Hashtable());
			 sessionTable 		= Hashtable.Synchronized(new Hashtable());
			
			Conf.Subscribe (typeof (Conf.WebServicesConfig), new Conf.ConfigUpdateHandler (WebServicesConfigurationChanged));							 
		}

		~WebServiceBackEnd() {
			resultTable.Clear();
			sessionTable.Clear();
		}
	
		public bool allowGlobalAccess {
			get { return web_global; }		
		}
				
		public static void init()
		{
		    if (instance == null) {

		  		instance = new WebServiceBackEnd();

  		  		//TCP Channel Listener registered in beagledWeb:init()
		  		//ChannelServices.RegisterChannel(new TcpChannel(8347));
		  		WellKnownServiceTypeEntry WKSTE = 
		  				new WellKnownServiceTypeEntry(typeof(WebServiceBackEnd),
				 			"WebServiceBackEnd.rem", WellKnownObjectMode.Singleton);
		  		RemotingConfiguration.ApplicationName="beagled";
		  		RemotingConfiguration.RegisterWellKnownServiceType(WKSTE);
		    }	 
		}

		void OnHitsAdded (QueryResult qres, ICollection hits)
		{
			if (resultTable.Contains(qres)) {
			
				SessionData sdata = ((SessionData) resultTable[qres]);	
				ArrayList results = sdata.results;
				bool localReq = sdata.localRequest;
			
				if (localReq){
					lock (results.SyncRoot) 
						results.AddRange(hits);
				}
				else {				
						//Query query = sdata.query;					
					lock (results.SyncRoot) {
						foreach (Hit h in hits) 
							if (h.UriAsString.StartsWith(NetworkedBeagle.BeagleNetPrefix) ||
											AccessFilter.FilterHit(h))
								results.Add(h);
					}
				}
			}
		}
		
		void removeUris(ArrayList results, ICollection uris)
		{
			foreach(Uri u in uris)
			   foreach(Hit h in results)
				if (h.Uri.Equals (u) && h.Uri.Fragment == u.Fragment) {
					lock (results.SyncRoot) {
						results.Remove(h);
					}
					break;
				}
		}

		void OnHitsSubtracted (QueryResult qres, ICollection uris)
		{
			if (resultTable.Contains(qres)) {
				SessionData sdata = ((SessionData) resultTable[qres]);	
				ArrayList results = sdata.results;
				removeUris(results, uris);
			}
		}

		void OnFinished (QueryResult qres)
		{
			DetachQueryResult (qres);
		}

		void OnCancelled (QueryResult qres)
		{
			DetachQueryResult (qres);
		}

		void AttachQueryResult (QueryResult qres, SessionData sdata)
		{
			if (qres != null) {

				qres.HitsAddedEvent += OnHitsAdded;
				qres.HitsSubtractedEvent += OnHitsSubtracted;
				qres.FinishedEvent += OnFinished;
				qres.CancelledEvent += OnCancelled;

				resultTable.Add(qres, sdata);
			}
		}

		void DetachQueryResult (QueryResult qres)
		{
			if (qres != null) {

				if (resultTable.Contains(qres)) {
					SessionData sdata = ((SessionData) resultTable[qres]);	
					sdata.results.Sort();
				}
				qres.HitsAddedEvent -= OnHitsAdded;
				qres.HitsSubtractedEvent -= OnHitsSubtracted;
				qres.FinishedEvent -= OnFinished;
				qres.CancelledEvent -= OnCancelled;

				resultTable.Remove(qres);
				qres.Dispose ();
			}
		}
		
		private class SessionData {
		
			private bool _localRequest;
			private ArrayList _results;
			private Query _query;
			
			public SessionData (Query _query, ArrayList _results, bool _localRequest)
			{
				this._localRequest = _localRequest;
				this._results = _results;
				this._query = _query;
			}

			public bool localRequest {
				get { return _localRequest; }
			}				
										
			public ArrayList results {
				get { return _results; }
			}
			

			public Query query  {
				get { return _query; }
			}
		}

		public string[] ICollection2StringList(ICollection il)
		{
			if (il == null)
				return new string[0] ;
			
			string[] sl = new string[il.Count];
						
			il.CopyTo(sl, 0);
				
			return sl;
		}
		
		private const int MAX_RESULTS_PER_CALL = 20;
		
		public const int SC_QUERY_SUCCESS = 0;
		public const int SC_INVALID_QUERY = -1;
		public const int SC_UNAUTHORIZED_ACCESS = -2;
		public const int SC_INVALID_SEARCH_TOKEN = -3;
		public const int SC_DUPLICATE_QUERY = -4;
		
		//Full beagledQuery
		public SearchResult doQuery(SearchRequest sreq, bool isLocalReq)
		{	
			SearchResult sr = null;

			if (sreq.text == null || sreq.text.Length == 0 ||
				(sreq.text.Length == 1 && sreq.text[0].Trim() == "") ) {
				
			    sr = new SearchResult();
			    sr.statusCode = SC_INVALID_QUERY;
			    sr.statusMsg = "Error: No search terms specified";
				return sr;
			}

			Query query = new Query();

			string searchString = "";						
			foreach (string text in sreq.text)  {
				query.AddText(text);
				searchString += (searchString.Length == 0) ? text:" " + text;
			}			
			
			Logger.Log.Info("WebServiceBackEnd: Received {0} WebService Query with search term: {1}", isLocalReq ? "Local":"External", searchString.Trim());

			if (sreq.mimeType != null && sreq.mimeType[0] != null)
				foreach (string mtype in sreq.mimeType)
					query.AddMimeType(mtype);

			if (sreq.searchSources != null && sreq.searchSources[0] != null)
				foreach (string src in sreq.searchSources)
					query.AddSource(src);

			//If needed, check to restrict queries to System or Neighborhood domain, can be added here
			if (sreq.qdomain > 0)
				query.AddDomain(sreq.qdomain);
						
			if (!isLocalReq) {	//External Request, check if this Node is already processing it

			 	lock (this) {					
					if ((sreq.searchId != 0) && NetworkedBeagle.IsCachedRequest(sreq.searchId)) {

						sr = new SearchResult();
				    	sr.numResults = sr.totalResults = sr.firstResultIndex = 0;
						sr.hitResults = new HitResult[sr.numResults];	
				 		sr.searchToken = "";

				 		sr.statusCode = SC_DUPLICATE_QUERY;
				 		sr.statusMsg = "Error: Duplicate Query loopback";
				 		Logger.Log.Warn("WebServiceBackEnd: Received duplicate Query for a query already in process!");
				 		Logger.Log.Warn("WebServiceBackEnd: Check NetBeagle configuration on all nodes to remove possible loops");
				 	}
	
					if (sreq.hopCount >= 5)  {
						//If request has traversed 5 nodes in reaching here, stop cascading. 
						//Make it a Local Query.
						query.RemoveDomain(sreq.qdomain);
						query.AddDomain(QueryDomain.System);
				 	}
				 					 	
					if ((sr == null) && (sreq.searchId != 0) )
						NetworkedBeagle.CacheRequest(query, sreq.searchId, sreq.hopCount + 1);				 	
				 }
				 
				 if (sr != null)
				 	return sr;	
				 	
				 //Logger.Log.Info("New external Query: searchId = {0}", sreq.searchId); 	
			}

			ArrayList results = ArrayList.Synchronized(new ArrayList());
			
			QueryResult qres = new QueryResult ();

			string searchToken = TokenGenerator();
						
			SessionData sdata = new SessionData(query, results, isLocalReq);
				
			AttachQueryResult (qres, sdata);
			
/* Include this code, if sessionID passed from front-end:
			if (sessionTable.Contains(searchToken))
				sessionTable[searchToken] = sdata;
			else
*/
			sessionTable.Add(searchToken, sdata);
			
			QueryDriver.DoQueryLocal (query, qres);
					
			while (resultTable.Contains(qres) && (results.Count < MAX_RESULTS_PER_CALL) )
				Thread.Sleep(100);

			//Console.WriteLine("WebServiceBackEnd: Got {0} results from beagled", results.Count);
			sr = new SearchResult();

			if (results.Count > 0)
			{ 
			  lock (results.SyncRoot) { //Lock results ArrayList to prevent more Hits added till we've processed doQuery
			
				sr.numResults = results.Count < MAX_RESULTS_PER_CALL ? results.Count: MAX_RESULTS_PER_CALL;	
				sr.hitResults = new HitResult[sr.numResults];
			    
			    string hitUri;			
				for (int i = 0; i < sr.numResults; i++) {
				
					Hit h = (Hit) results[i];

					string snippet; 
						
					//Queryable queryable = h.SourceObject as Queryable;
					Queryable queryable = QueryDriver.GetQueryable (h.SourceObjectName);
					
					if (queryable == null)
						snippet = "ERROR: hit.SourceObject is null, uri=" + h.Uri;
					else
						snippet = queryable.GetSnippet (ICollection2StringList(query.StemmedText), h);											
								
					sr.hitResults[i] = new HitResult();
					
					hitUri = h.UriAsString;
					if (isLocalReq || hitUri.StartsWith(NetworkedBeagle.BeagleNetPrefix))
							sr.hitResults[i].uri = hitUri;
					else
							sr.hitResults[i].uri = AccessFilter.TranslateHit(h);
					
	        	    sr.hitResults[i].resourceType = h.Type;
					sr.hitResults[i].mimeType = h.MimeType;
					sr.hitResults[i].source = h.Source;
					sr.hitResults[i].score = h.Score;				
				
					int plen = h.Properties.Count;
					sr.hitResults[i].properties = new HitProperty[plen];
					for (int j = 0; j < plen; j++) {
						Property p = (Property) h.Properties[j];
						sr.hitResults[i].properties[j] = new HitProperty();
						sr.hitResults[i].properties[j].PKey = p.Key;
						sr.hitResults[i].properties[j].PVal = p.Value;				
						sr.hitResults[i].properties[j].IsMutable = p.IsMutable;				
						sr.hitResults[i].properties[j].IsSearched = p.IsSearched;							
					}

					sr.hitResults[i].hashCode = h.GetHashCode ();
					
					if (snippet != null)
						sr.hitResults[i].snippet = snippet.Trim();
				}					
			   } //end lock
			 }// end if
			 else {

			    sr.numResults = 0;
				sr.hitResults = new HitResult[sr.numResults];	
			 }

			 sr.totalResults = results.Count;
			 
			 sr.firstResultIndex = 0;			 
			 sr.searchToken = "";
				
			 if (sr.totalResults > 0)
				sr.searchToken = searchToken;
					
			 sr.statusCode = SC_QUERY_SUCCESS;
			 sr.statusMsg = "Success";
			 Logger.Log.Info("WebServiceBackEnd: Total Results = "  + sr.totalResults);			
			 return sr;
		}

		public SearchResult getResults(GetResultsRequest req, bool isLocalReq)
		{										
			int startIndex = req.startIndex;
			string searchToken = req.searchToken;
						
			SearchResult sr = new SearchResult();
			sr.numResults = 0;
			
			if (!sessionTable.ContainsKey(searchToken)) {
				sr.statusCode = SC_INVALID_SEARCH_TOKEN;
				sr.statusMsg = "Error: Invalid Search Token";
				Logger.Log.Warn("GetResults: Invalid Search Token received ");
				return sr;
			}
									
			ArrayList results = ((SessionData)sessionTable[searchToken]).results;
			if (results == null) {
				sr.statusCode = SC_INVALID_SEARCH_TOKEN;
				sr.statusMsg = "Error: Invalid Search Token";
				Logger.Log.Warn("GetResults: Invalid Search Token received ");
				return sr;
			}

			lock (results.SyncRoot) { //Lock results ArrayList to prevent more Hits getting added till we've processed doQuery
 
 				int i = 0;
 				
				if (startIndex < results.Count)
					sr.numResults = (results.Count < startIndex + MAX_RESULTS_PER_CALL) ? (results.Count - startIndex): MAX_RESULTS_PER_CALL;
				
				sr.hitResults = new HitResult[sr.numResults];
			
				string hitUri;
				for (int k = startIndex; (i < sr.numResults) && (k < results.Count); k++)   {		
				
					Hit h = (Hit) results[k];	
							
					sr.hitResults[i] = new HitResult();
					
// GetResults will NOT return Snippets by default. Client must make explicit GetSnippets request to get snippets for these hits.
// Not initializing sr.hitResults[i].snippet implies there is no <snippets> element in HitResult XML response.
							
					hitUri = h.UriAsString;
					if (isLocalReq || hitUri.StartsWith(NetworkedBeagle.BeagleNetPrefix))
							sr.hitResults[i].uri = hitUri;
					else
							sr.hitResults[i].uri = AccessFilter.TranslateHit(h);

	        	    sr.hitResults[i].resourceType = h.Type;
					sr.hitResults[i].mimeType = h.MimeType;
					sr.hitResults[i].source = h.Source;
					sr.hitResults[i].score = h.Score;
									
					int plen = h.Properties.Count;
					sr.hitResults[i].properties = new HitProperty[plen];
					for (int j = 0; j < plen; j++) {
						Property p = (Property) h.Properties[j];
						sr.hitResults[i].properties[j] = new HitProperty();
						sr.hitResults[i].properties[j].PKey = p.Key;
						sr.hitResults[i].properties[j].PVal = p.Value;				
						sr.hitResults[i].properties[j].IsMutable = p.IsMutable;				
						sr.hitResults[i].properties[j].IsSearched = p.IsSearched;							
					}												
					
					sr.hitResults[i].hashCode = h.GetHashCode ();
					
					i++;
				}												
			} //end lock

			sr.totalResults = results.Count;
													
			sr.firstResultIndex = startIndex;
			sr.searchToken = "";
			
			if (sr.totalResults > 0)
				sr.searchToken = searchToken;
				
			sr.statusCode = SC_QUERY_SUCCESS;
			sr.statusMsg = "Success";
			//Console.WriteLine("WebServiceQuery: Total Results = "  + sr.totalResults);	
			return sr;
		}
		
		public static string InvalidHitSnippetError = "ERROR: Invalid or Duplicate Hit Id";
		public HitSnippet[] getSnippets(GetSnippetsRequest req)
		{	
			HitSnippet[] response;
			string searchToken = req.searchToken;
			int[] hitHashCodes = req.hitHashCodes;
			
			if (!sessionTable.ContainsKey(searchToken)) {
			
				response = new HitSnippet[0];
				Logger.Log.Warn("GetSnippets: Invalid Search Token received ");
				return response;
			}
									
			ArrayList results = ((SessionData)sessionTable[searchToken]).results;
			if ((results == null) || (results.Count == 0)) {

				response = new HitSnippet[0];
				Logger.Log.Warn("GetSnippets: Invalid Search Token received ");
				return response;
			}
			
			int i = 0; 		
			ArrayList hashCodeList = new ArrayList();
			hashCodeList.AddRange(hitHashCodes);
				
			response = new HitSnippet[hitHashCodes.Length];
			Logger.Log.Debug("GetSnippets invoked with {0} hitHashCodes", hitHashCodes.Length);

			Query query = ((SessionData)sessionTable[searchToken]).query;
			
			lock (results.SyncRoot)  {
			
				string snippet = null; 
				foreach (Hit h in results)  {
				
					int hashCode = h.GetHashCode();
					if (hashCodeList.Contains(hashCode)) {
					
							hashCodeList.Remove(hashCode);	

							//Queryable queryable = h.SourceObject as Queryable;
							Queryable queryable = QueryDriver.GetQueryable (h.SourceObjectName);													

							if (queryable == null)
								snippet = "ERROR: hit.SourceObject is null, uri=" + h.Uri;
							else
								snippet = queryable.GetSnippet (ICollection2StringList(query.StemmedText), h);		
							
							//GetSnippets always invoked on Target Beagle Node where hits originate:
							if (snippet == null)
								snippet = "";
								
							HitSnippet hs = new HitSnippet();
							hs.hashCode = hashCode; 
							hs.snippet = snippet.Trim();
							response[i++] = hs;		
					
							if ((hashCodeList.Count == 0) || (i == hitHashCodes.Length))
								return response;
					}
				} //end foreach
			} //end lock
			
			foreach (int hashCode in hashCodeList) {
					HitSnippet hs = new HitSnippet();
					hs.hashCode = hashCode; 
					hs.snippet = InvalidHitSnippetError;
					response[i++] = hs;	

					if (i == hitHashCodes.Length)
							break;
			}		
			Logger.Log.Warn("GetSnippets invoked some invalid hitIds");
			
			return response;
		}
		
		//Returns a 15-char random alpha-numeric string similar to ASP.NET sessionId
		private string TokenGenerator()
		{
			const int TOKEN_LEN = 15;

			Random r = new Random();
			string token = ((Char)((int)((26 * r.NextDouble()) + 'a')) + System.Guid.NewGuid().ToString()).Substring (0, TOKEN_LEN);

			char alpha = (Char)((int)((26 * r.NextDouble()) + 'a'));
				
			return (token.Replace('-', alpha));
		}
	}	


////////////////////////////////////////////////////////////////////////////////////////////
/////////////   WebService Request-Response Data Structures   	
////////////////////////////////////////////////////////////////////////////////////////////

/* These are duplicate definitions to the ones in WebServiceProxy.cs. 
    So, we will define and use these from one central place: WebServiceProxy.cs

	[Serializable()]
	public class SearchRequest  {

		public string[] text;
		public string[] mimeType;
		public string[] searchSources;
		public QueryDomain qdomain;
		//Unique searchId across network
		public int 	searchId;			
		public int 	hopCount;
	}

	[Serializable()]
	public class HitProperty  {

		private string pKey="";
		public string PKey  {
			get {return pKey;}
			set {pKey = value;}
		}

		private string pVal="";
		public string PVal  {
			get {return pVal;}
			set {pVal = value;}
		}
		
		private bool  isMutable;
		public bool IsMutable {
			get { return isMutable; }
			set { isMutable = value; }
		}
		
		private bool   isSearched;
		public bool IsSearched {
			get { return isSearched; }
			set { isSearched = value; }
		}		
	}

	[Serializable()]
	public class HitResult {

		public string 	uri;
		//public string 	parentUri;		
		public string 	resourceType;
		public string 	mimeType;
		public string 	source;
		public double 	score;
		public HitProperty[] properties;
		//FIXME: public xxx[] data;
		public int 	hashCode;
		public string 	snippet;
	}

	[Serializable()]
	public class SearchResult  {
	
		public int statusCode;			//ReturnCode for programmatic processing
		public string statusMsg;		//User-friendly return message

		public string searchToken;		//Token identifying the query,
										//to enable follow-up queries
		public int firstResultIndex; 	//Index of first result in this response
		public int numResults;		 	//No. of results in this response
		public int totalResults;		//Total no. of results from the query
		public HitResult[] hitResults;
	}

	[Serializable()]
	public class HitSnippet {
		public int hashCode;
		public string snippet;
	}
*/	
		
}
