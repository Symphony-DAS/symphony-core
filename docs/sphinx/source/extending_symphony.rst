.. include:: global.rst

.. _extending-symphony-chapter:

==================
Extending Symphony
==================

|core| is built on a modular, plugin-based architecture. This chapter describes common features of all plugins for the |core| framework. In addition, this chapter describes development of plugins for each of the available extension points:

* `Unit Converters`_
* `Hardware Bridges`_
* `External Devices`_
* `Persistence Backends`_


Unit Converters
===============

|core| does not automatically discover available unit converters. To register a unit conversion procedure, register the ``ConvertProc`` with the ``Converters`` singleton::

    Converters.Register(fromUnits, toUnits, proc)
    
where ``proc`` is a ``Symphony.Core.ConvertProc`` that takes and returns a single |measurement|.

Hardware Bridges
================

|core| does not currently support extending the available hardware bridges.


External Devices
================

|core| does not currently support extending the available External Device types.

Persistence Backends
====================

|core| does not currently support extending the persistence backends.



