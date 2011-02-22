"""
 * Copyright (c) Contributors http://github.com/aduffy70/vpcsimlog
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the vpcsimlog webapp nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
"""

import cgi
from google.appengine.ext import webapp
from google.appengine.ext.webapp.util import run_wsgi_app
from google.appengine.ext import db
import time


class HtmlPage():
    """
    Class containing basic html page layout.  #TODO- Either go all the way with this structure or just drop it
    """
    header = '<html><body>'

    footer = '</body></html>'


class LogOrParametersPage(webapp.RequestHandler):
    """
    Page to select either the parameters form or log form.
    """
    def get(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        self.response.out.write(self.instructions)
        self.response.out.write(self.form)
        self.response.out.write(page.footer)

    instructions = """
        <p>
            <b>Welcome to the virtual plant community simulations (vpcSim) web application.</b>
        </p>
        From this page you can:
        <ul>
            <li>change the parameters controlling a virtual plant community,</li>
            <li>change only the disturbance pattern, or </li>
            <li>view data logged by the plant community.</li>
        </ul>
        <hr>
        """

    form = """
        <form enctype="multipart/form-data" action="/parametersform1" method="get">
           <input type="submit" value="Change all parameters" style="width: 175px">
        </form>
        <form enctype="multipart/form-data" action="/selectmap" method="get">
           <input type="submit" value="Change disturbance only" style="width: 175px">
        </form>
        <form enctype="multipart/form-data" action="/log" method="get">
            <input type="submit" value="View log data" style="width: 175px">
        </form>
        """


class SimulationLogObject(db.Model):
    """
    Record class representing the log output from a single step of a community simulation.  Includes a timestamp in case a single step was visualized multiple times and a region tag in case the same simulation id was used on more than one region.
    """
    # Time record was created
    time_stamp = db.DateTimeProperty(auto_now_add=True)

    # Simulation ID of the simulation that created this record
    sim_id = db.StringProperty()

    #Region tag of the opensim region that created this record
    region_tag = db.StringProperty()

    # CSV string of simulation step and counts for each species
    data = db.StringProperty()


class AddLogRecord(webapp.RequestHandler):
    """
    Stores logged output from a community simulation.  Accessed by the opensim region module.
    """
    def get(self):
        record = SimulationLogObject()
        sim_id = self.request.get('sim_id')
        region_tag = self.request.get('region_tag')
        data = self.request.get('data')
        if ((len(sim_id) == 10) and (len(data.split(',')) == 7)):
            record.sim_id = sim_id
            record.region_tag = region_tag
            record.data = data
            record.put()
            self.response.out.write('SUCCESS')
        else:
            #Send some return value to tell the region that this failed
            self.response.out.write('FAILED')


class LogFormPage(webapp.RequestHandler):
    """
    A page to request log data by simulation id and region tag.  Accessed by the user by url or hyperlink.
    """
    def get(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        self.response.out.write(self.form)
        self.response.out.write(page.footer)

    form = """
        <form enctype="multipart/form-data" action="/getlog" method="post">
            <p>
                <b>Specify a Simulation ID and region tag to view log records: </b><br>
                Note- the simulation ID and region tag are displayed on the vpcHUD.<br><br>
                Simulation ID: <input type="text" name="sim_id" maxlength="10" size="11"> &nbsp;&nbsp;&nbsp;&nbsp;
                Region tag: <input type="text" name="region_tag" maxlength="20" size="21"><br><br>
                <input type="submit" value="Get log records">
            </p>
        </form>
        """


class GetLogRecords(webapp.RequestHandler):
    """
    Retrieves the log data requested on the log page.  Accessed by submitting the LogFormPage.
    """
    def post(self):
        page = HtmlPage()
        self.response.out.write(page.header)
        sim_id = self.request.get('sim_id')
        region_tag = self.request.get('region_tag')
        records = db.GqlQuery("SELECT * FROM SimulationLogObject WHERE sim_id=:1 AND region_tag=:2 ORDER BY time_stamp", sim_id, region_tag)
        if (records.count(1) > 0):
            self.response.out.write(self.record_output_label % (sim_id, region_tag))
            for record in records:
                self.response.out.write(self.record_output % (record.data, str(record.time_stamp)))
        else:
            self.response.out.write(self.no_records_output % (sim_id, region_tag))
        self.response.out.write(page.footer)

    record_output_label = """
        <p>
            <b>Records for %s in %s:</b>
        </p>
        <b>Simulation step, Gap count, Species1 count, Species2 count, Species3 count, Species4 count, Species5 count</b><br>
        """

    record_output = '%s,%s<br>'

    no_records_output = 'No records for %s in %s.'


class DeleteLogRecords(webapp.RequestHandler):
    """
    Deletes all log records with a specific simulation id and region tag.  Accessed by the opensim region module.
    """
    def get(self):
        sim_id = self.request.get('sim_id')
        region_tag = self.request.get('region_tag')
        records = db.GqlQuery("SELECT * FROM SimulationLogObject WHERE sim_id=:1 AND region_tag=:2", sim_id, region_tag)
        if (records.count(1) > 0):
            for record in records:
                record.delete()
        self.response.out.write('SUCCESS')

"""
END SECTION: Community logging
"""

# url to class mapping
application = webapp.WSGIApplication([
    ('/', LogFormPage),
    ('/addlog', AddLogRecord),
    ('/getlog', GetLogRecords),
    ('/deletelog', DeleteLogRecords)], debug=True)

def main():
    run_wsgi_app(application)

if __name__ == "__main__":
    main()
