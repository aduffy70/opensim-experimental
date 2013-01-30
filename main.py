#!/usr/bin/env python
from google.appengine.ext import webapp
from google.appengine.ext.webapp.util import run_wsgi_app
from google.appengine.ext import db
from google.appengine.api import users
from google.appengine.ext.webapp import template

class VisitRecord(db.Model):
    """
    Visit record format.
    """
    #google account authorized to view the records
    account = db.StringProperty()
    #opensim region that created the record
    region = db.StringProperty()
    #name of the visiting avatar
    name = db.StringProperty()
    #visit datetime provided by the region (good for humans, bad for sorting)
    local_datetime = db.StringProperty()
    #DateTime recorded by this app (good for sorting, bad for humans - utc)
    global_datetime = db.DateTimeProperty(auto_now=True)


class MainHandler(webapp.RequestHandler):
    """
    Main page with log records in reverse chronological order.
    """
    def get(self):
        user = users.get_current_user()
        if not user:
            self.redirect(users.create_login_url(self.request.uri))
        else:
            visit_records = db.GqlQuery("""SELECT * FROM VisitRecord WHERE
                                      account=:1 ORDER BY global_datetime
                                      DESC""", user.email())
            if (visit_records.count(1) > 0):
                template_values = {'visit_records': visit_records}
                self.response.out.write(template.render('mainpage.html', template_values))
            else:
                self.response.out.write("No visit records")


class LogVisitHandler(webapp.RequestHandler):
    """
    Accepts new records from the VisitLogger Region Module.
    """
    def get(self):
        new_visit_record = VisitRecord()
        new_visit_record.account = self.request.get('account')
        new_visit_record.region = self.request.get('region')
        new_visit_record.name = self.request.get('name')
        new_visit_record.local_datetime = self.request.get('datetime')
        new_visit_record.put()


class ClearLogHandler(webapp.RequestHandler):
    """
    Clears log records for a specific account.
    """
    def post(self):
        user = users.get_current_user()
        if not user:
            self.redirect(users.create_login_url(self.request.uri))
        else:
            visit_records = db.GqlQuery("""SELECT * FROM VisitRecord WHERE
                                        account=:1""", user.email())
            if (visit_records.count(1) > 0):
                for visit_record in visit_records:
                    visit_record.delete()
            self.redirect('/')


def main():
    application = webapp.WSGIApplication([('/', MainHandler),
                                          ('/logvisit', LogVisitHandler),
                                          ('/clearlog', ClearLogHandler)],
                                         debug=True)
    run_wsgi_app(application)


if __name__ == '__main__':
    main()
