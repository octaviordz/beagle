//
// beagleQueue.js: Queue component implementation
//
// Copyright (C) 2007 Pierre Östlund
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

var queue_add = Components.classes ['@mozilla.org/supports-array;1']
	.createInstance (Components.interfaces.nsISupportsArray);
var queue_remove = Components.classes ['@mozilla.org/supports-array;1']
	.createInstance (Components.interfaces.nsISupportsArray);
var observerService = Components.classes ['@mozilla.org/observer-service;1']
	.getService(Components.interfaces.nsIObserverService);

var totalAdded = 0;
var totalRemoved = 0;

function init ()
{
	observerService.addObserver (gBeagleQueueObserver, 'quit-application', false);
}

// obj is either nsIMsgDBHdr or nsIMsgFolder
function add (obj)
{
	if (queue_add.GetIndexOf (obj) != -1)
		return;
	
	var indexer = Components.classes ['@beagle-project.org/services/indexer;1']
		.getService (Components.interfaces.nsIBeagleIndexer);
	
	if (obj instanceof Components.interfaces.nsIMsgDBHdr)
		indexer.markHdrAsIndexed (obj);
	else if (obj instanceof Components.interfaces.nsIMsgFolder)
		indexer.markFolderAsIndexed (obj);
	else 
		return;
	
	queue_add.AppendElement (obj);
	totalAdded++;
}

function remove (obj)
{
	if (queue_remove.GetIndexOf (obj) != -1)
		return;
	
	var indexer = Components.classes ['@beagle-project.org/services/indexer;1']
		.getService (Components.interfaces.nsIBeagleIndexer);
	
	if (obj instanceof Components.interfaces.nsIMsgDBHdr)
		indexer.resetHdr (obj);
	else if (obj instanceof Components.interfaces.nsIMsgFolder)
		indexer.resetFolder (obj, false, false, false);
	else 
		return;
	
	queue_remove.AppendElement (obj);
	totalRemoved++;
}

// add, remove* and move* all return true if the object was added to the queue. If the object was
// rejected by a filter, then they will return false. A filter in this sense is if a mail is marked
// to be indexed or not (by the user).

// Add a new header for inclusion in the beagle index
function addHdr (hdr)
{
	var indexer = Components.classes ['@beagle-project.org/services/indexer;1']
		.getService (Components.interfaces.nsIBeagleIndexer);
	
	// Check if we should index this
	if (!indexer.shouldIndexHdr (hdr) || !indexer.shouldIndexFolder (hdr.folder)) 
		return false;

	// Add header to queue (no duplicats though)
	/*if (queue_add.GetIndexOf (hdr) == -1)  {
		indexer.markHdrAsIndexed (hdr);
		queue_add.AppendElement (hdr);
		totalAdded++;
	}*/
	add (hdr);
	
	process ();
	
	return true;
}

function removeHdr (hdr)
{
	var indexer = Components.classes ['@beagle-project.org/services/indexer;1']
		.getService (Components.interfaces.nsIBeagleIndexer);
	
	if (hdr instanceof Components.interfaces.nsIMsgDBHdr) {
		/*if (queue_remove.GetIndexOf (hdr) == -1) {
			indexer.markHdrAsIndexed (hdr);
			queue_remove.AppendElement (hdr);
			totalRemoved++;
		}*/
		remove (hdr);

		process ();

		return true;
	}
	
	return false;
}

function removeFolder (folder)
{
	var indexer = Components.classes ['@beagle-project.org/services/indexer;1']
		.getService (Components.interfaces.nsIBeagleIndexer);
	
	if (folder instanceof Components.interfaces.nsIMsgFolder) {
		if (queue_remove.GetIndexOf (folder) == -1) {
			indexer.markFolderAsIndexed (folder);
			queue_remove.AppendElement (folder);
			totalRemoved++;
		}

		process ();

		return true;
	}
	
	return false;
}

function moveHdr (oldHdr, newHdr)
{
	var indexer = Components.classes ['@beagle-project.org/services/indexer;1']
		.getService (Component.interfaces.nsIBeagleIndexer);
	
	if (!indexer.shouldIndexHdr (oldHdr) || !indexer.shouldIndexHdr (newHdr))
		return false;
	
	if (queue_remove.GetIndexOf (oldHdr) == -1) {
		indexer.markHdrAsIndexed (oldHdr);
		queue_remove.AppendElement (oldHdr);
		totalRemoved++;
	}
	
	if (queue_add.GetIndexOf (newHdr) == -1) {
		indexer.markHdrAsIndexer (newHdr);
		queue_add.AppendElement (newHdr);
		totalAdded++;
	}
	processs ();
	
	return true;
}

function moveFolder (oldFolder, newFolder)
{
	var indexer = Components.classes ['@beagle-project.org/services/indexer;1']
		.getService (Component.interfaces.nsIBeagleIndexer);
	
	if (!indexer.shouldIndexFolder (oldFolder) || !indexer.shouldIndexFolder (newFolder))
		return false;
	
	if (queue_remove.GetIndexOf (oldFolder) == -1) {
		indexer.markFolderAsIndexed (oldFolder);
		queue_remove.AppendElement (oldFolder);
		totalRemoved++;
	}
	
	if (queue_add.GetIndexOf (newFolder) == -1) {
		indexer.markFolderAsIndexed (newFolder);
		queue_add.AppendElement (newFolder);
		totalAdded++;
	}
	process ();
		
	return true;
}

// This process function will make sure that we have enough objects in the queue before processing
function process ()
{
	var settings = Components.classes ['@beagle-project.org/services/settings;1']
		.getService (Components.interfaces.nsIBeagleSettings);
	
	if (getQueueCount () < settings.getIntPref ('IndexQueueCount'))
		return;
	
	forceProcess ();
}

// No object count is done here, mainly so that the queue can be processed at any given time
function forceProcess ()
{
	var count = getQueueCount ();
	if (count == 0)
		return;
		
	var indexer = Components.classes ['@beagle-project.org/services/indexer;1']
		.getService (Components.interfaces.nsIBeagleIndexer);
	
	// Add new items to the beagle database
	for (var i = 0; i < queue_add.Count (); i++) {
		var msg = queue_add.GetElementAt (i).QueryInterface (Components.interfaces.nsIMsgDBHdr);
		if (!msg)
			continue;
		indexer.addToIndex (msg);
	}
	
	// Remove old items from the beagle database
	for (var i = 0; i < queue_remove.Count (); i++) {
		var obj = queue_remove.GetElementAt (i);
		
		if (obj instanceof Components.interfaces.nsIMsgDBHdr) {
			obj.QueryInterface (Components.interfaces.nsIMsgDBHdr);
			indexer.dropHdrFromIndex (obj);
		} else if (obj instanceof Components.interfaces.nsIMsgFolder) {
			obj.QueryInterface (Components.interfaces.nsIMsgFolder);
			indexer.dropFolderFromIndex (obj);
		}
	}
	
	queue_add.Clear ();
	queue_remove.Clear ();
	
	dump ("Done processing " + count + " items\n");
}

function getQueueCount ()
{
	return queue_add.Count () + queue_remove.Count ();
}

// This observer will check if the application is about to quit and process any remaining
// items in the queue when it does
var gBeagleQueueObserver = {

	observe: function (subject, topic, data)
	{
		// Just process whatever is left in the queue
		try {
			forceProcess ();
			observerService.removeObserver (this, 'quit-application');
		} catch (ex) {
		}
	}
};
