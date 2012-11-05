.. include:: global.rst

================
Data Persistence
================

|core| manages persistence of experimental data during acquisition. To facilitate import into a data management system such as `Ovation <http://physionconsulting.com/web/Ovation.html>`_, |core| maintains all information present in the internal data model (see :ref:`data-model`) in the saved files. The |core| persistence mechanism is modular, allowing selection of different file format modules. This chapter describes the two included persistence backends:

* :ref:`XML <xml-backend-section>`
* :ref:`HDF5 <hdf5-backend-section>`

For information about adding additional persistence backends, see :ref:`extending-symphony-chapter`.

.. _xml-backend-section:

XML Backend
===========

The XML persistence backend writes experimental data to a .Net ``XmlWriter``. To use the XML persistence backend, pass an instance of ``EpochXMLPersistor`` to ``Controller.RunEpoch``::

    Controller controller = ...
    Epoch epoch= ...
    
    var xmlWriter = XmlWriter.Create("<filename>");
    var xmlPersistor = new EpochXMLPersistor(xmlWriter);
    
    xmlPersistor.BeginEpochGroup(...);
    controller.RunEpoch(epoch, xmlPersistor);
    xmlPersistor.EndEpochGorup();
    

Figure :ref:`4.1 <hdf5-format-figure>` shows the XML format used by the XML backend.

.. warning:: The XML persistence backend is intended for development only. For production (experiment) use, it is recommended that you use the :ref:`HDF5 backend <hdf5-backend-section>`.

.. _xml-format-figure:
.. figure:: images/symphony-xml-format.pdf
    :width: 95%
    
    XML backend format.

.. _hdf5-backend-section:

HDF5 Backend
============

The HDF5 persistence backend writes experimental data to an HDF5 [#]_ data store. To use the HDF5 persistence backend to save data, pass an instance of the ``EpochHDF5Persistor`` class to ``Controller.RunEpoch``::

    Controller controller = ...
    Epoch epoch = ...
    
    var persistor = new EpochHDF5Persistor("<filename>", "<file prefix>");
    
    persistor.BeginEpochGroup(...);
    
    controller.RunEpoch(epoch, persistor);
    
    persistor.EndEpochGroup();


Figure :ref:`4.2 <hdf5-format-figure>` shows the 

.. _hdf5-format-figure:
.. figure:: images/symphony-hdf5-format.pdf
    :width: 100%
    
    HDF5 backend format.


.. [#] http://www.hdfgroup.org/HDF5/

